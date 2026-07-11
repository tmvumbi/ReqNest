using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ReqNest.Api.Assistant;

public sealed record AssistantToolCall(string Id, string Name, string ArgumentsJson);

public sealed record AssistantStreamResult(
    string Content,
    IReadOnlyList<AssistantToolCall> ToolCalls,
    string? FinishReason);

// Minimal OpenRouter chat-completions client (OpenAI-compatible wire format)
// supporting SSE streaming and tool calls.
public sealed class OpenRouterClient(IHttpClientFactory httpClientFactory, IConfiguration configuration)
{
    private const string Endpoint = "https://openrouter.ai/api/v1/chat/completions";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);

    public string Model => configuration["Ai:Model"] ?? "anthropic/claude-sonnet-4.5";

    private string? ApiKey => configuration["Ai:OpenRouterApiKey"];

    public async Task<AssistantStreamResult> StreamChatAsync(
        JsonArray messages,
        JsonArray? tools,
        Func<string, Task> onDelta,
        CancellationToken cancellationToken)
    {
        using var request = BuildRequest(messages, tools, stream: true);
        var client = httpClientFactory.CreateClient(nameof(OpenRouterClient));
        using var response = await client.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"OpenRouter request failed ({(int)response.StatusCode}): {Truncate(body)}");
        }

        var content = new StringBuilder();
        var toolCalls = new SortedDictionary<int, (string Id, string Name, StringBuilder Arguments)>();
        string? finishReason = null;
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (!line.StartsWith("data:", StringComparison.Ordinal))
            {
                continue;
            }

            var payload = line[5..].Trim();
            if (payload.Length == 0 || payload == "[DONE]")
            {
                continue;
            }

            JsonNode? node;
            try
            {
                node = JsonNode.Parse(payload);
            }
            catch (JsonException)
            {
                continue;
            }

            var choice = node?["choices"]?[0];
            if (choice is null)
            {
                continue;
            }

            finishReason = choice["finish_reason"]?.GetValue<string>() ?? finishReason;
            var delta = choice["delta"];
            var text = delta?["content"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(text))
            {
                content.Append(text);
                await onDelta(text);
            }

            if (delta?["tool_calls"] is JsonArray deltaCalls)
            {
                foreach (var deltaCall in deltaCalls)
                {
                    var index = deltaCall?["index"]?.GetValue<int>() ?? 0;
                    if (!toolCalls.TryGetValue(index, out var accumulated))
                    {
                        accumulated = (string.Empty, string.Empty, new StringBuilder());
                    }

                    var id = deltaCall?["id"]?.GetValue<string>();
                    var name = deltaCall?["function"]?["name"]?.GetValue<string>();
                    var arguments = deltaCall?["function"]?["arguments"]?.GetValue<string>();
                    toolCalls[index] = (
                        string.IsNullOrEmpty(id) ? accumulated.Id : id,
                        string.IsNullOrEmpty(name) ? accumulated.Name : name,
                        accumulated.Arguments.Append(arguments));
                }
            }
        }

        var calls = toolCalls.Values
            .Where(call => call.Name.Length > 0)
            .Select((call, index) => new AssistantToolCall(
                call.Id.Length > 0 ? call.Id : $"call_{index}",
                call.Name,
                call.Arguments.Length > 0 ? call.Arguments.ToString() : "{}"))
            .ToArray();
        return new AssistantStreamResult(content.ToString(), calls, finishReason);
    }

    public async Task<string> CompleteAsync(JsonArray messages, CancellationToken cancellationToken)
    {
        using var request = BuildRequest(messages, tools: null, stream: false);
        var client = httpClientFactory.CreateClient(nameof(OpenRouterClient));
        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"OpenRouter request failed ({(int)response.StatusCode}): {Truncate(body)}");
        }

        var node = JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return node?["choices"]?[0]?["message"]?["content"]?.GetValue<string>() ?? string.Empty;
    }

    private HttpRequestMessage BuildRequest(JsonArray messages, JsonArray? tools, bool stream)
    {
        // Clone: callers reuse the same arrays across tool rounds, and a JsonNode
        // can only have one parent.
        var payload = new JsonObject
        {
            ["model"] = Model,
            ["messages"] = messages.DeepClone(),
            ["stream"] = stream,
        };
        if (tools is { Count: > 0 })
        {
            payload["tools"] = tools.DeepClone();
            payload["tool_choice"] = "auto";
        }

        var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {ApiKey}");
        request.Headers.TryAddWithoutValidation("HTTP-Referer", "https://reqnest.local");
        request.Headers.TryAddWithoutValidation("X-Title", "ReqNest Assistant");
        return request;
    }

    private static string Truncate(string value) => value.Length > 400 ? value[..400] : value;
}
