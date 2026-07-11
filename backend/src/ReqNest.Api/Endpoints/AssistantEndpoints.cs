using System.Text;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using ReqNest.Api.Assistant;
using ReqNest.Core.Assistant;
using ReqNest.Infrastructure.Persistence;

namespace ReqNest.Api.Endpoints;

public static class AssistantEndpoints
{
    private const string DefaultTitle = "New conversation";
    private const int MaxToolRounds = 8;
    private const int HistoryLimit = 60;

    public static IEndpointRouteBuilder MapAssistantEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/assistant")
            .RequireAuthorization()
            .WithTags("AI assistant");
        group.MapGet("/conversations", ListConversationsAsync);
        group.MapPost("/conversations", CreateConversationAsync);
        group.MapGet("/conversations/{conversationId:guid}", GetConversationAsync);
        group.MapDelete("/conversations/{conversationId:guid}", DeleteConversationAsync);
        group.MapPost("/conversations/{conversationId:guid}/messages", SendMessageAsync);
        group.MapPost("/conversations/{conversationId:guid}/transcripts", SaveTranscriptAsync);
        group.MapPost("/realtime/session", CreateRealtimeSessionAsync);

        // MCP server over HTTP (JSON-RPC 2.0): tools/list and tools/call scoped to the caller.
        endpoints.MapPost("/api/mcp", HandleMcpAsync)
            .RequireAuthorization()
            .WithTags("AI assistant");
        return endpoints;
    }

    private static async Task<IResult> ListConversationsAsync(
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        if (authorization is null)
        {
            return ApiProblems.TenantRequired(httpContext);
        }

        var userId = httpContext.User.UserId();
        var conversations = await dbContext.AiConversations.AsNoTracking()
            .Where(entity => entity.UserId == userId)
            .OrderByDescending(entity => entity.LastMessageAt)
            .Take(100)
            .Select(entity => new AssistantConversationSummary(
                entity.Id,
                entity.Title.Length == 0 ? DefaultTitle : entity.Title,
                entity.LastMessageAt,
                entity.CreatedAt))
            .ToArrayAsync(cancellationToken);
        return TypedResults.Ok(conversations);
    }

    private static async Task<IResult> CreateConversationAsync(
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        if (authorization is null)
        {
            return ApiProblems.TenantRequired(httpContext);
        }

        var conversation = new AiConversation
        {
            TenantId = authorization.TenantId,
            UserId = httpContext.User.UserId(),
            Title = string.Empty,
        };
        dbContext.AiConversations.Add(conversation);
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(new AssistantConversationSummary(
            conversation.Id, DefaultTitle, conversation.LastMessageAt, conversation.CreatedAt));
    }

    private static async Task<IResult> GetConversationAsync(
        Guid conversationId,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var conversation = await FindConversationAsync(conversationId, httpContext, dbContext, cancellationToken);
        if (conversation is null)
        {
            return ApiProblems.NotFound(httpContext, "Conversation");
        }

        var messages = await dbContext.AiChatMessages.AsNoTracking()
            .Where(entity => entity.ConversationId == conversationId)
            .OrderBy(entity => entity.CreatedAt)
            .ToArrayAsync(cancellationToken);
        var visible = messages
            .Where(message => message.Role is "user" or "assistant" && message.Content.Length > 0)
            .Select(message => new AssistantMessageResponse(
                message.Id, message.Role, message.Content, message.IsVoice, message.CreatedAt))
            .ToArray();
        return TypedResults.Ok(new AssistantConversationDetail(
            conversation.Id,
            conversation.Title.Length == 0 ? DefaultTitle : conversation.Title,
            conversation.LastMessageAt,
            visible));
    }

    private static async Task<IResult> DeleteConversationAsync(
        Guid conversationId,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var conversation = await FindConversationAsync(conversationId, httpContext, dbContext, cancellationToken);
        if (conversation is null)
        {
            return ApiProblems.NotFound(httpContext, "Conversation");
        }

        dbContext.AiConversations.Remove(conversation);
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task SendMessageAsync(
        Guid conversationId,
        AssistantSendMessageRequest request,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        OpenRouterClient openRouter,
        AssistantToolService toolService,
        CancellationToken cancellationToken)
    {
        var response = httpContext.Response;
        var conversation = await FindConversationAsync(conversationId, httpContext, dbContext, cancellationToken);
        if (conversation is null || string.IsNullOrWhiteSpace(request.Content))
        {
            response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        response.Headers.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache";
        response.Headers.Append("X-Accel-Buffering", "no");

        if (!openRouter.IsConfigured)
        {
            await WriteEventAsync(response, "error", new JsonObject
            {
                ["message"] = "The AI assistant is not configured. Set Ai:OpenRouterApiKey in appsettings.Local.json.",
            }, cancellationToken);
            return;
        }

        var authorization = httpContext.TenantAuthorization()!;
        var userName = await dbContext.Users.AsNoTracking()
            .Where(entity => entity.Id == conversation.UserId)
            .Select(entity => entity.DisplayName)
            .SingleOrDefaultAsync(cancellationToken) ?? "the user";

        var userMessage = new AiChatMessage
        {
            TenantId = authorization.TenantId,
            ConversationId = conversation.Id,
            Role = "user",
            Content = request.Content.Trim(),
        };
        conversation.LastMessageAt = DateTimeOffset.UtcNow;
        dbContext.AiChatMessages.Add(userMessage);
        await dbContext.SaveChangesAsync(cancellationToken);

        var history = await dbContext.AiChatMessages.AsNoTracking()
            .Where(entity => entity.ConversationId == conversation.Id)
            .OrderByDescending(entity => entity.CreatedAt)
            .Take(HistoryLimit)
            .OrderBy(entity => entity.CreatedAt)
            .ToListAsync(cancellationToken);
        var messages = BuildModelMessages(userName, history);
        var tools = toolService.ToOpenAiTools();

        try
        {
            var finalContent = string.Empty;
            for (var round = 0; round <= MaxToolRounds; round++)
            {
                var result = await openRouter.StreamChatAsync(
                    messages,
                    tools,
                    async delta => await WriteEventAsync(
                        response, "delta", new JsonObject { ["text"] = delta }, cancellationToken),
                    cancellationToken);

                if (result.ToolCalls.Count == 0 || round == MaxToolRounds)
                {
                    finalContent = result.Content;
                    break;
                }

                var toolCallsJson = new JsonArray();
                foreach (var call in result.ToolCalls)
                {
                    toolCallsJson.Add(new JsonObject
                    {
                        ["id"] = call.Id,
                        ["type"] = "function",
                        ["function"] = new JsonObject
                        {
                            ["name"] = call.Name,
                            ["arguments"] = call.ArgumentsJson,
                        },
                    });
                }

                dbContext.AiChatMessages.Add(new AiChatMessage
                {
                    TenantId = authorization.TenantId,
                    ConversationId = conversation.Id,
                    Role = "assistant",
                    Content = result.Content,
                    ToolCallsJson = toolCallsJson.ToJsonString(),
                });
                messages.Add(new JsonObject
                {
                    ["role"] = "assistant",
                    ["content"] = result.Content.Length > 0 ? result.Content : null,
                    ["tool_calls"] = toolCallsJson.DeepClone(),
                });

                foreach (var call in result.ToolCalls)
                {
                    await WriteEventAsync(response, "tool", new JsonObject
                    {
                        ["name"] = call.Name,
                    }, cancellationToken);
                    var toolResult = await toolService.ExecuteAsync(
                        httpContext, call.Name, call.ArgumentsJson, cancellationToken);
                    dbContext.AiChatMessages.Add(new AiChatMessage
                    {
                        TenantId = authorization.TenantId,
                        ConversationId = conversation.Id,
                        Role = "tool",
                        Content = toolResult,
                        ToolCallId = call.Id,
                        ToolName = call.Name,
                    });
                    messages.Add(new JsonObject
                    {
                        ["role"] = "tool",
                        ["tool_call_id"] = call.Id,
                        ["content"] = toolResult,
                    });
                }

                await dbContext.SaveChangesAsync(cancellationToken);
            }

            var assistantMessage = new AiChatMessage
            {
                TenantId = authorization.TenantId,
                ConversationId = conversation.Id,
                Role = "assistant",
                Content = finalContent,
            };
            conversation.LastMessageAt = DateTimeOffset.UtcNow;
            dbContext.AiChatMessages.Add(assistantMessage);

            if (conversation.Title.Length == 0)
            {
                conversation.Title = await GenerateTitleAsync(
                    openRouter, userMessage.Content, finalContent, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await WriteEventAsync(response, "done", new JsonObject
            {
                ["messageId"] = assistantMessage.Id.ToString(),
                ["title"] = conversation.Title.Length == 0 ? DefaultTitle : conversation.Title,
                ["lastMessageAt"] = conversation.LastMessageAt.ToString("O"),
            }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Client went away mid-stream; nothing to report.
        }
        catch (Exception exception)
        {
            await WriteEventAsync(response, "error", new JsonObject
            {
                ["message"] = exception.Message,
            }, CancellationToken.None);
        }
    }

    private static async Task<IResult> SaveTranscriptAsync(
        Guid conversationId,
        AssistantTranscriptRequest request,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var conversation = await FindConversationAsync(conversationId, httpContext, dbContext, cancellationToken);
        if (conversation is null)
        {
            return ApiProblems.NotFound(httpContext, "Conversation");
        }

        if (request.Role is not ("user" or "assistant") || string.IsNullOrWhiteSpace(request.Content))
        {
            return ApiProblems.Validation(httpContext, "A transcript needs a user/assistant role and text.");
        }

        var message = new AiChatMessage
        {
            TenantId = conversation.TenantId,
            ConversationId = conversation.Id,
            Role = request.Role,
            Content = request.Content.Trim(),
            IsVoice = true,
        };
        conversation.LastMessageAt = DateTimeOffset.UtcNow;
        if (conversation.Title.Length == 0)
        {
            conversation.Title = "Voice conversation";
        }

        dbContext.AiChatMessages.Add(message);
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(new AssistantMessageResponse(
            message.Id, message.Role, message.Content, message.IsVoice, message.CreatedAt));
    }

    private static async Task<IResult> CreateRealtimeSessionAsync(
        HttpContext httpContext,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        if (authorization is null)
        {
            return ApiProblems.TenantRequired(httpContext);
        }

        var apiKey = configuration["Ai:OpenAiApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return ApiProblems.Validation(
                httpContext,
                "Voice conversations need an OpenAI API key. Set Ai:OpenAiApiKey in appsettings.Local.json.",
                "voice_not_configured");
        }

        var model = configuration["Ai:RealtimeModel"] ?? "gpt-realtime";
        var payload = new JsonObject
        {
            ["session"] = new JsonObject
            {
                ["type"] = "realtime",
                ["model"] = model,
                ["instructions"] =
                    "You are the ReqNest help desk voice assistant. Be concise and helpful. " +
                    "You cannot perform actions in voice mode; suggest using the text chat for actions.",
                ["audio"] = new JsonObject
                {
                    ["input"] = new JsonObject
                    {
                        ["transcription"] = new JsonObject { ["model"] = "whisper-1" },
                    },
                },
            },
        };
        var client = httpClientFactory.CreateClient("openai-realtime");
        using var request = new HttpRequestMessage(
            HttpMethod.Post, "https://api.openai.com/v1/realtime/client_secrets")
        {
            Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");
        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return ApiProblems.Validation(
                httpContext, $"Could not create a realtime session: {(int)response.StatusCode}.", "voice_session_failed");
        }

        var node = JsonNode.Parse(body);
        var secret = node?["value"]?.GetValue<string>() ?? node?["client_secret"]?["value"]?.GetValue<string>();
        if (secret is null)
        {
            return ApiProblems.Validation(httpContext, "The realtime session response was invalid.", "voice_session_failed");
        }

        return TypedResults.Ok(new AssistantRealtimeSessionResponse(secret, model));
    }

    private static async Task<IResult> HandleMcpAsync(
        JsonObject request,
        HttpContext httpContext,
        AssistantToolService toolService,
        CancellationToken cancellationToken)
    {
        var id = request["id"]?.DeepClone();
        var method = request["method"]?.GetValue<string>();
        JsonObject response = new() { ["jsonrpc"] = "2.0", ["id"] = id };
        switch (method)
        {
            case "initialize":
                response["result"] = new JsonObject
                {
                    ["protocolVersion"] = "2025-06-18",
                    ["capabilities"] = new JsonObject { ["tools"] = new JsonObject() },
                    ["serverInfo"] = new JsonObject { ["name"] = "reqnest", ["version"] = "1.0" },
                };
                break;
            case "tools/list":
                var tools = new JsonArray();
                foreach (var tool in toolService.Tools)
                {
                    tools.Add(new JsonObject
                    {
                        ["name"] = tool.Name,
                        ["description"] = tool.Description,
                        ["inputSchema"] = tool.Parameters.DeepClone(),
                    });
                }

                response["result"] = new JsonObject { ["tools"] = tools };
                break;
            case "tools/call":
                var name = request["params"]?["name"]?.GetValue<string>() ?? string.Empty;
                var arguments = request["params"]?["arguments"]?.ToJsonString() ?? "{}";
                var result = await toolService.ExecuteAsync(httpContext, name, arguments, cancellationToken);
                response["result"] = new JsonObject
                {
                    ["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = result }),
                };
                break;
            default:
                response["error"] = new JsonObject { ["code"] = -32601, ["message"] = "Method not found" };
                break;
        }

        return TypedResults.Ok(response);
    }

    private static List<JsonObject> BuildModelMessagesCore(string userName)
    {
        return
        [
            new JsonObject
            {
                ["role"] = "system",
                ["content"] =
                    "You are the ReqNest assistant, embedded in the ReqNest help desk application. " +
                    $"You are talking to {userName}. Today is {DateTimeOffset.UtcNow:yyyy-MM-dd}.\n\n" +
                    "Use the available tools to look up real data before answering questions about tickets, " +
                    "projects, members, or knowledge articles — never invent data. You may take actions " +
                    "(create tickets, comment, change status, update fields) when the user asks.\n\n" +
                    "Rules:\n" +
                    "- Before performing an action, make sure you have all required inputs. If anything is " +
                    "missing or ambiguous (which project, what title, which assignee, …) ask the user a short, " +
                    "specific question instead of guessing.\n" +
                    "- After an action, confirm what was done and link to it.\n" +
                    "- Whenever you mention a ticket, project, or knowledge article that has an appUrl in a " +
                    "tool result, format it as a markdown link to that path, e.g. [HELP-12](/app/tickets/1234).\n" +
                    "- Answer in the user's language. Format responses in Markdown (lists, bold, short " +
                    "paragraphs). Keep answers concise.\n" +
                    "- You only see what this user is allowed to see; if a tool reports access errors, say so.",
            },
        ];
    }

    private static JsonArray BuildModelMessages(string userName, List<AiChatMessage> history)
    {
        var messages = new JsonArray();
        foreach (var message in BuildModelMessagesCore(userName))
        {
            messages.Add(message);
        }

        foreach (var message in history)
        {
            switch (message.Role)
            {
                case "user":
                    messages.Add(new JsonObject { ["role"] = "user", ["content"] = message.Content });
                    break;
                case "assistant" when message.ToolCallsJson is not null:
                    messages.Add(new JsonObject
                    {
                        ["role"] = "assistant",
                        ["content"] = message.Content.Length > 0 ? message.Content : null,
                        ["tool_calls"] = JsonNode.Parse(message.ToolCallsJson),
                    });
                    break;
                case "assistant":
                    messages.Add(new JsonObject { ["role"] = "assistant", ["content"] = message.Content });
                    break;
                case "tool":
                    messages.Add(new JsonObject
                    {
                        ["role"] = "tool",
                        ["tool_call_id"] = message.ToolCallId,
                        ["content"] = message.Content,
                    });
                    break;
            }
        }

        return messages;
    }

    private static async Task<string> GenerateTitleAsync(
        OpenRouterClient openRouter,
        string userMessage,
        string assistantMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            var messages = new JsonArray
            {
                new JsonObject
                {
                    ["role"] = "system",
                    ["content"] =
                        "Generate a very short title (3 to 6 words, no quotes, no trailing punctuation) " +
                        "summarizing this help desk conversation. Reply with the title only, in the " +
                        "conversation's language.",
                },
                new JsonObject
                {
                    ["role"] = "user",
                    ["content"] = $"User: {Truncate(userMessage)}\nAssistant: {Truncate(assistantMessage)}",
                },
            };
            var title = (await openRouter.CompleteAsync(messages, cancellationToken)).Trim().Trim('"');
            return title.Length is > 0 and <= 200 ? title : DefaultTitle;
        }
        catch (Exception)
        {
            return DefaultTitle;
        }
    }

    private static async Task<AiConversation?> FindConversationAsync(
        Guid conversationId,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        if (authorization is null)
        {
            return null;
        }

        var userId = httpContext.User.UserId();
        return await dbContext.AiConversations.SingleOrDefaultAsync(
            entity => entity.Id == conversationId && entity.UserId == userId,
            cancellationToken);
    }

    private static async Task WriteEventAsync(
        HttpResponse response,
        string eventName,
        JsonObject data,
        CancellationToken cancellationToken)
    {
        await response.WriteAsync($"event: {eventName}\ndata: {data.ToJsonString()}\n\n", cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
    }

    private static string Truncate(string value) => value.Length > 600 ? value[..600] : value;
}

public sealed record AssistantConversationSummary(
    Guid Id,
    string Title,
    DateTimeOffset LastMessageAt,
    DateTimeOffset CreatedAt);

public sealed record AssistantMessageResponse(
    Guid Id,
    string Role,
    string Content,
    bool IsVoice,
    DateTimeOffset CreatedAt);

public sealed record AssistantConversationDetail(
    Guid Id,
    string Title,
    DateTimeOffset LastMessageAt,
    IReadOnlyCollection<AssistantMessageResponse> Messages);

public sealed record AssistantSendMessageRequest(string Content);

public sealed record AssistantTranscriptRequest(string Role, string Content);

public sealed record AssistantRealtimeSessionResponse(string ClientSecret, string Model);
