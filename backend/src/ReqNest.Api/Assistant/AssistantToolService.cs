using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ReqNest.Api.Assistant;

public sealed record AssistantTool(string Name, string Description, JsonObject Parameters);

// MCP-style tool server. Tools run by replaying the caller's credentials against the
// public API, so the assistant can only see and do what the current user can.
public sealed class AssistantToolService(IHttpClientFactory httpClientFactory)
{
    public IReadOnlyList<AssistantTool> Tools { get; } = BuildTools();

    public JsonArray ToOpenAiTools()
    {
        var array = new JsonArray();
        foreach (var tool in Tools)
        {
            array.Add(new JsonObject
            {
                ["type"] = "function",
                ["function"] = new JsonObject
                {
                    ["name"] = tool.Name,
                    ["description"] = tool.Description,
                    ["parameters"] = tool.Parameters.DeepClone(),
                },
            });
        }

        return array;
    }

    public async Task<string> ExecuteAsync(
        HttpContext context,
        string name,
        string argumentsJson,
        CancellationToken cancellationToken)
    {
        JsonObject arguments;
        try
        {
            arguments = JsonNode.Parse(argumentsJson) as JsonObject ?? [];
        }
        catch (JsonException)
        {
            arguments = [];
        }

        try
        {
            using var client = CreateClient(context);
            return name switch
            {
                "list_projects" => await ListProjectsAsync(client, cancellationToken),
                "search_tickets" => await SearchTicketsAsync(client, arguments, cancellationToken),
                "get_ticket" => await GetTicketAsync(client, arguments, cancellationToken),
                "create_ticket" => await CreateTicketAsync(client, arguments, cancellationToken),
                "update_ticket" => await UpdateTicketAsync(client, arguments, cancellationToken),
                "add_comment" => await AddCommentAsync(client, arguments, cancellationToken),
                "transition_ticket" => await TransitionTicketAsync(client, arguments, cancellationToken),
                "search_knowledge" => await SearchKnowledgeAsync(client, arguments, cancellationToken),
                "list_members" => await ListMembersAsync(client, cancellationToken),
                "get_ticket_schema" => await GetTicketSchemaAsync(client, arguments, cancellationToken),
                _ => Error($"Unknown tool '{name}'."),
            };
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or TaskCanceledException)
        {
            return Error(exception.Message);
        }
    }

    private HttpClient CreateClient(HttpContext context)
    {
        var client = httpClientFactory.CreateClient(nameof(AssistantToolService));
        client.BaseAddress = new Uri($"{context.Request.Scheme}://{context.Request.Host}");
        client.Timeout = TimeSpan.FromSeconds(30);
        if (context.Request.Headers.TryGetValue("Authorization", out var authorization))
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", (string?)authorization);
        }

        if (context.Request.Headers.TryGetValue("X-Tenant-Id", out var tenant))
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-Tenant-Id", (string?)tenant);
        }

        return client;
    }

    private static async Task<string> ListProjectsAsync(HttpClient client, CancellationToken ct)
    {
        var projects = await GetJsonAsync(client, "/api/projects", ct);
        if (projects is not JsonArray array)
        {
            return Error("Could not load projects.");
        }

        var result = new JsonArray();
        foreach (var project in array)
        {
            result.Add(new JsonObject
            {
                ["id"] = project?["id"]?.DeepClone(),
                ["key"] = project?["key"]?.DeepClone(),
                ["name"] = project?["name"]?.DeepClone(),
                ["description"] = project?["description"]?.DeepClone(),
                ["isArchived"] = project?["isArchived"]?.DeepClone(),
                ["appUrl"] = $"/app/projects/{project?["id"]?.GetValue<string>()}",
            });
        }

        return new JsonObject { ["projects"] = result }.ToJsonString();
    }

    private static async Task<string> SearchTicketsAsync(HttpClient client, JsonObject args, CancellationToken ct)
    {
        var query = new StringBuilder("/api/tickets?pageSize=15");
        if (Str(args, "query") is { } search)
        {
            query.Append("&search=").Append(Uri.EscapeDataString(search));
        }

        if (Str(args, "projectId") is { } projectId)
        {
            query.Append("&projectId=").Append(Uri.EscapeDataString(projectId));
        }

        if (Str(args, "queue") is { } queue)
        {
            query.Append("&queue=").Append(Uri.EscapeDataString(queue));
        }

        var page = await GetJsonAsync(client, query.ToString(), ct);
        var items = page?["items"] as JsonArray ?? [];
        var result = new JsonArray();
        foreach (var item in items)
        {
            result.Add(CompactTicket(item));
        }

        return new JsonObject
        {
            ["total"] = page?["total"]?.DeepClone(),
            ["tickets"] = result,
        }.ToJsonString();
    }

    private static async Task<string> GetTicketAsync(HttpClient client, JsonObject args, CancellationToken ct)
    {
        var reference = Str(args, "ticket") ?? Str(args, "key") ?? Str(args, "id");
        if (reference is null)
        {
            return Error("Provide the ticket key (e.g. HELP-12) or id.");
        }

        var detail = await ResolveTicketAsync(client, reference, ct);
        if (detail is null)
        {
            return Error($"Ticket '{reference}' was not found or you do not have access to it.");
        }

        var comments = await GetJsonAsync(client, $"/api/tickets/{detail["id"]}/comments", ct) as JsonArray ?? [];
        var recentComments = new JsonArray();
        foreach (var comment in comments.Skip(Math.Max(0, comments.Count - 6)))
        {
            recentComments.Add(new JsonObject
            {
                ["author"] = comment?["authorDisplayName"]?.DeepClone(),
                ["body"] = comment?["bodyPlainText"]?.DeepClone() ?? comment?["body"]?.DeepClone(),
                ["createdAt"] = comment?["createdAt"]?.DeepClone(),
            });
        }

        var ticket = CompactTicket(detail);
        ticket["description"] = detail["description"]?.DeepClone();
        ticket["labels"] = detail["labels"]?.DeepClone();
        ticket["dueAt"] = detail["dueAt"]?.DeepClone();
        ticket["reporter"] = detail["reporterDisplayName"]?.DeepClone();
        ticket["resolutionSummary"] = detail["resolutionSummary"]?.DeepClone();
        ticket["recentComments"] = recentComments;
        return ticket.ToJsonString();
    }

    private static async Task<string> CreateTicketAsync(HttpClient client, JsonObject args, CancellationToken ct)
    {
        var projectReference = Str(args, "project") ?? Str(args, "projectKey") ?? Str(args, "projectId");
        var title = Str(args, "title");
        var description = Str(args, "description");
        if (projectReference is null || title is null || description is null)
        {
            return Error("create_ticket requires project, title, and description.");
        }

        var project = await ResolveProjectAsync(client, projectReference, ct);
        if (project is null)
        {
            return Error($"Project '{projectReference}' was not found. Use list_projects to see available projects.");
        }

        Guid? assigneeUserId = null;
        if (Str(args, "assignee") is { } assignee)
        {
            var member = await ResolveMemberAsync(client, assignee, ct);
            if (member is null)
            {
                return Error($"No member matches '{assignee}'. Use list_members to see who is available.");
            }

            assigneeUserId = Guid.Parse(member["userId"]!.GetValue<string>());
        }

        var typeKey = Str(args, "typeKey") ?? "Incident";
        var priorityKey = Str(args, "priorityKey") ?? "Normal";
        var labels = args["labels"] as JsonArray ?? [];
        var payload = new JsonObject
        {
            ["projectId"] = project["id"]!.GetValue<string>(),
            ["title"] = title,
            ["description"] = description,
            ["type"] = LegacyType(typeKey),
            ["priority"] = LegacyPriority(priorityKey),
            ["assigneeUserId"] = assigneeUserId?.ToString(),
            ["labels"] = labels.DeepClone(),
            ["dueAt"] = Str(args, "dueAt"),
            ["typeKey"] = typeKey,
            ["priorityKey"] = priorityKey,
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/tickets")
        {
            Content = JsonContent(payload),
        };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.NewGuid().ToString());
        using var response = await client.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            return ApiError("Creating the ticket failed", response.StatusCode, body);
        }

        var created = JsonNode.Parse(body);
        return new JsonObject
        {
            ["created"] = true,
            ["ticket"] = CompactTicket(created),
        }.ToJsonString();
    }

    private static async Task<string> UpdateTicketAsync(HttpClient client, JsonObject args, CancellationToken ct)
    {
        var reference = Str(args, "ticket") ?? Str(args, "key") ?? Str(args, "id");
        if (reference is null)
        {
            return Error("Provide the ticket key or id to update.");
        }

        var detail = await ResolveTicketAsync(client, reference, ct);
        if (detail is null)
        {
            return Error($"Ticket '{reference}' was not found.");
        }

        Guid? assigneeUserId = detail["assigneeUserId"]?.GetValue<string>() is { } current ? Guid.Parse(current) : null;
        if (Str(args, "assignee") is { } assignee)
        {
            if (assignee.Equals("none", StringComparison.OrdinalIgnoreCase) ||
                assignee.Equals("unassigned", StringComparison.OrdinalIgnoreCase))
            {
                assigneeUserId = null;
            }
            else
            {
                var member = await ResolveMemberAsync(client, assignee, ct);
                if (member is null)
                {
                    return Error($"No member matches '{assignee}'. Use list_members to see who is available.");
                }

                assigneeUserId = Guid.Parse(member["userId"]!.GetValue<string>());
            }
        }

        var typeKey = Str(args, "typeKey") ?? detail["typeKey"]?.GetValue<string>() ?? "Incident";
        var priorityKey = Str(args, "priorityKey") ?? detail["priorityKey"]?.GetValue<string>() ?? "Normal";
        var payload = new JsonObject
        {
            ["title"] = Str(args, "title") ?? detail["title"]?.GetValue<string>(),
            ["description"] = Str(args, "description") ?? detail["description"]?.GetValue<string>(),
            ["type"] = LegacyType(typeKey),
            ["priority"] = LegacyPriority(priorityKey),
            ["assigneeUserId"] = assigneeUserId?.ToString(),
            ["labels"] = (args["labels"] as JsonArray)?.DeepClone() ?? detail["labels"]?.DeepClone() ?? new JsonArray(),
            ["dueAt"] = Str(args, "dueAt") ?? detail["dueAt"]?.GetValue<string>(),
            ["resolutionSummary"] = Str(args, "resolutionSummary") ?? detail["resolutionSummary"]?.GetValue<string>(),
            ["version"] = detail["version"]?.DeepClone(),
            ["typeKey"] = typeKey,
            ["priorityKey"] = priorityKey,
        };
        using var response = await client.PatchAsync($"/api/tickets/{detail["id"]}", JsonContent(payload), ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            return ApiError("Updating the ticket failed", response.StatusCode, body);
        }

        return new JsonObject { ["updated"] = true, ["ticket"] = CompactTicket(JsonNode.Parse(body)) }.ToJsonString();
    }

    private static async Task<string> AddCommentAsync(HttpClient client, JsonObject args, CancellationToken ct)
    {
        var reference = Str(args, "ticket") ?? Str(args, "key") ?? Str(args, "id");
        var body = Str(args, "body") ?? Str(args, "comment");
        if (reference is null || body is null)
        {
            return Error("add_comment requires the ticket key/id and the comment body.");
        }

        var detail = await ResolveTicketAsync(client, reference, ct);
        if (detail is null)
        {
            return Error($"Ticket '{reference}' was not found.");
        }

        var payload = new JsonObject
        {
            ["body"] = body,
            ["mentionUserIds"] = new JsonArray(),
        };
        using var response = await client.PostAsync($"/api/tickets/{detail["id"]}/comments", JsonContent(payload), ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            return ApiError("Adding the comment failed", response.StatusCode, responseBody);
        }

        return new JsonObject
        {
            ["commented"] = true,
            ["ticket"] = CompactTicket(detail),
        }.ToJsonString();
    }

    private static async Task<string> TransitionTicketAsync(HttpClient client, JsonObject args, CancellationToken ct)
    {
        var reference = Str(args, "ticket") ?? Str(args, "key") ?? Str(args, "id");
        var toStatus = Str(args, "toStatus") ?? Str(args, "status");
        if (reference is null || toStatus is null)
        {
            return Error("transition_ticket requires the ticket key/id and the target status.");
        }

        var detail = await ResolveTicketAsync(client, reference, ct);
        if (detail is null)
        {
            return Error($"Ticket '{reference}' was not found.");
        }

        var workflows = await GetJsonAsync(client, "/api/workflows", ct) as JsonArray ?? [];
        var statusId = detail["statusId"]?.GetValue<string>();
        JsonArray? statuses = null;
        foreach (var workflow in workflows)
        {
            var candidates = workflow?["statuses"] as JsonArray;
            if (candidates?.Any(status => status?["id"]?.GetValue<string>() == statusId) == true)
            {
                statuses = candidates;
                break;
            }
        }

        if (statuses is null)
        {
            return Error("Could not resolve the ticket's workflow.");
        }

        var target = statuses.FirstOrDefault(status =>
            string.Equals(status?["key"]?.GetValue<string>(), toStatus, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status?["label"]?.GetValue<string>(), toStatus, StringComparison.OrdinalIgnoreCase));
        if (target is null)
        {
            var available = string.Join(", ", statuses.Select(status => status?["label"]?.GetValue<string>()));
            return Error($"Status '{toStatus}' does not exist in this workflow. Available statuses: {available}.");
        }

        var payload = new JsonObject
        {
            ["toStatusId"] = target["id"]!.GetValue<string>(),
            ["comment"] = Str(args, "comment"),
            ["version"] = detail["version"]?.DeepClone(),
        };
        using var response = await client.PostAsync($"/api/tickets/{detail["id"]}/transition", JsonContent(payload), ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            return ApiError(
                "The transition was rejected (it may not be allowed from the current status, or a comment may be required)",
                response.StatusCode,
                body);
        }

        return new JsonObject
        {
            ["transitioned"] = true,
            ["ticket"] = CompactTicket(JsonNode.Parse(body)),
        }.ToJsonString();
    }

    private static async Task<string> SearchKnowledgeAsync(HttpClient client, JsonObject args, CancellationToken ct)
    {
        var query = Str(args, "query");
        var url = "/api/knowledge" + (query is null ? string.Empty : $"?search={Uri.EscapeDataString(query)}");
        var articles = await GetJsonAsync(client, url, ct) as JsonArray ?? [];
        var result = new JsonArray();
        foreach (var article in articles.Take(10))
        {
            result.Add(new JsonObject
            {
                ["id"] = article?["id"]?.DeepClone(),
                ["title"] = article?["title"]?.DeepClone(),
                ["status"] = article?["status"]?.DeepClone(),
                ["appUrl"] = $"/app/knowledge/{article?["id"]?.GetValue<string>()}",
            });
        }

        return new JsonObject { ["articles"] = result }.ToJsonString();
    }

    private static async Task<string> ListMembersAsync(HttpClient client, CancellationToken ct)
    {
        var members = await GetJsonAsync(client, "/api/members", ct) as JsonArray ?? [];
        var result = new JsonArray();
        foreach (var member in members)
        {
            result.Add(new JsonObject
            {
                ["userId"] = member?["userId"]?.DeepClone(),
                ["displayName"] = member?["displayName"]?.DeepClone(),
                ["email"] = member?["email"]?.DeepClone(),
                ["status"] = member?["status"]?.DeepClone(),
            });
        }

        return new JsonObject { ["members"] = result }.ToJsonString();
    }

    private static async Task<string> GetTicketSchemaAsync(HttpClient client, JsonObject args, CancellationToken ct)
    {
        var projectId = Str(args, "projectId");
        var url = "/api/configuration/ticket-schema" +
                  (projectId is null ? string.Empty : $"?projectId={Uri.EscapeDataString(projectId)}");
        var schema = await GetJsonAsync(client, url, ct);
        return new JsonObject
        {
            ["types"] = Compact(schema?["types"] as JsonArray),
            ["priorities"] = Compact(schema?["priorities"] as JsonArray),
        }.ToJsonString();

        static JsonArray Compact(JsonArray? items)
        {
            var result = new JsonArray();
            foreach (var item in items ?? [])
            {
                if (item?["isActive"]?.GetValue<bool>() == false)
                {
                    continue;
                }

                result.Add(new JsonObject
                {
                    ["key"] = item?["key"]?.DeepClone(),
                    ["label"] = item?["label"]?.DeepClone(),
                });
            }

            return result;
        }
    }

    private static async Task<JsonObject?> ResolveTicketAsync(HttpClient client, string reference, CancellationToken ct)
    {
        if (Guid.TryParse(reference, out var id))
        {
            return await GetJsonAsync(client, $"/api/tickets/{id}", ct) as JsonObject;
        }

        var page = await GetJsonAsync(
            client, $"/api/tickets?pageSize=5&search={Uri.EscapeDataString(reference)}", ct);
        var match = (page?["items"] as JsonArray)?.FirstOrDefault(item =>
            string.Equals(item?["key"]?.GetValue<string>(), reference, StringComparison.OrdinalIgnoreCase))
            ?? (page?["items"] as JsonArray)?.FirstOrDefault();
        var matchId = match?["id"]?.GetValue<string>();
        return matchId is null ? null : await GetJsonAsync(client, $"/api/tickets/{matchId}", ct) as JsonObject;
    }

    private static async Task<JsonObject?> ResolveProjectAsync(HttpClient client, string reference, CancellationToken ct)
    {
        var projects = await GetJsonAsync(client, "/api/projects", ct) as JsonArray ?? [];
        return projects.FirstOrDefault(project =>
            string.Equals(project?["id"]?.GetValue<string>(), reference, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(project?["key"]?.GetValue<string>(), reference, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(project?["name"]?.GetValue<string>(), reference, StringComparison.OrdinalIgnoreCase)) as JsonObject;
    }

    private static async Task<JsonObject?> ResolveMemberAsync(HttpClient client, string reference, CancellationToken ct)
    {
        var members = await GetJsonAsync(client, "/api/members", ct) as JsonArray ?? [];
        return members.FirstOrDefault(member =>
            string.Equals(member?["email"]?.GetValue<string>(), reference, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(member?["displayName"]?.GetValue<string>(), reference, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(member?["userId"]?.GetValue<string>(), reference, StringComparison.OrdinalIgnoreCase)) as JsonObject;
    }

    private static JsonObject CompactTicket(JsonNode? ticket) => new()
    {
        ["id"] = ticket?["id"]?.DeepClone(),
        ["key"] = ticket?["key"]?.DeepClone(),
        ["title"] = ticket?["title"]?.DeepClone(),
        ["project"] = ticket?["projectName"]?.DeepClone(),
        ["status"] = ticket?["statusLabel"]?.DeepClone(),
        ["priority"] = ticket?["priorityKey"]?.DeepClone() ?? ticket?["priority"]?.DeepClone(),
        ["type"] = ticket?["typeKey"]?.DeepClone() ?? ticket?["type"]?.DeepClone(),
        ["assignee"] = ticket?["assigneeDisplayName"]?.DeepClone(),
        ["updatedAt"] = ticket?["updatedAt"]?.DeepClone(),
        ["appUrl"] = $"/app/tickets/{ticket?["id"]?.GetValue<string>()}",
    };

    private static async Task<JsonNode?> GetJsonAsync(HttpClient client, string url, CancellationToken ct)
    {
        using var response = await client.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return JsonNode.Parse(await response.Content.ReadAsStringAsync(ct));
    }

    private static StringContent JsonContent(JsonObject payload) =>
        new(payload.ToJsonString(), Encoding.UTF8, "application/json");

    private static string? Str(JsonObject args, string key) =>
        args[key] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text)
            ? text.Trim()
            : null;

    private static string LegacyType(string key) =>
        new[] { "Incident", "ServiceRequest", "Task", "Problem" }
            .FirstOrDefault(value => value.Equals(key, StringComparison.OrdinalIgnoreCase)) ?? "Incident";

    private static string LegacyPriority(string key) =>
        new[] { "Low", "Normal", "High", "Urgent" }
            .FirstOrDefault(value => value.Equals(key, StringComparison.OrdinalIgnoreCase)) ?? "Normal";

    private static string Error(string message) =>
        new JsonObject { ["error"] = message }.ToJsonString();

    private static string ApiError(string prefix, System.Net.HttpStatusCode status, string body)
    {
        string? detail = null;
        try
        {
            detail = JsonNode.Parse(body)?["detail"]?.GetValue<string>();
        }
        catch (JsonException)
        {
            // Non-JSON error body; fall through with the generic message.
        }

        return Error($"{prefix}: {(int)status} {detail ?? Truncate(body)}");
    }

    private static string Truncate(string value) => value.Length > 300 ? value[..300] : value;

    private static List<AssistantTool> BuildTools()
    {
        return
        [
            new AssistantTool(
                "list_projects",
                "List the projects the current user can access, with keys and names.",
                Schema([])),
            new AssistantTool(
                "search_tickets",
                "Search tickets by free text (matches key, title, description, labels, comments). " +
                "Optional queue filter: my-open, unassigned, recently-updated, todo, in-progress, overdue, sla-risk, done-recently.",
                Schema(new()
                {
                    ["query"] = ("string", "Free-text search or a ticket key like HELP-12."),
                    ["projectId"] = ("string", "Restrict to a project id (from list_projects)."),
                    ["queue"] = ("string", "Optional queue filter."),
                })),
            new AssistantTool(
                "get_ticket",
                "Get the full detail of one ticket, including description and recent comments.",
                Schema(new() { ["ticket"] = ("string", "Ticket key (e.g. HELP-12) or id.") }, required: ["ticket"])),
            new AssistantTool(
                "create_ticket",
                "Create a new ticket. Ask the user for any missing required detail before calling this.",
                Schema(new()
                {
                    ["project"] = ("string", "Project key, name, or id."),
                    ["title"] = ("string", "Short ticket title."),
                    ["description"] = ("string", "Full description of the request or issue."),
                    ["typeKey"] = ("string", "Ticket type key from get_ticket_schema (default Incident)."),
                    ["priorityKey"] = ("string", "Priority key from get_ticket_schema (default Normal)."),
                    ["assignee"] = ("string", "Assignee email or display name (optional)."),
                    ["dueAt"] = ("string", "Due date in ISO 8601 (optional)."),
                }, required: ["project", "title", "description"])),
            new AssistantTool(
                "update_ticket",
                "Update fields on an existing ticket (title, description, priority, assignee, due date, labels).",
                Schema(new()
                {
                    ["ticket"] = ("string", "Ticket key or id."),
                    ["title"] = ("string", "New title (optional)."),
                    ["description"] = ("string", "New description (optional)."),
                    ["priorityKey"] = ("string", "New priority key (optional)."),
                    ["assignee"] = ("string", "Assignee email/name, or 'none' to unassign (optional)."),
                    ["dueAt"] = ("string", "New due date ISO 8601 (optional)."),
                    ["resolutionSummary"] = ("string", "Resolution summary (optional)."),
                }, required: ["ticket"])),
            new AssistantTool(
                "add_comment",
                "Add a comment to a ticket on behalf of the current user.",
                Schema(new()
                {
                    ["ticket"] = ("string", "Ticket key or id."),
                    ["body"] = ("string", "Comment text."),
                }, required: ["ticket", "body"])),
            new AssistantTool(
                "transition_ticket",
                "Move a ticket to another workflow status (e.g. In progress, Done). " +
                "Some transitions require a comment; pass one when asked.",
                Schema(new()
                {
                    ["ticket"] = ("string", "Ticket key or id."),
                    ["toStatus"] = ("string", "Target status key or label."),
                    ["comment"] = ("string", "Optional transition comment."),
                }, required: ["ticket", "toStatus"])),
            new AssistantTool(
                "search_knowledge",
                "Search the internal knowledge base articles.",
                Schema(new() { ["query"] = ("string", "Search text.") })),
            new AssistantTool(
                "list_members",
                "List workspace members (names and emails), e.g. to resolve an assignee.",
                Schema([])),
            new AssistantTool(
                "get_ticket_schema",
                "Get the available ticket types and priorities (keys and labels).",
                Schema(new() { ["projectId"] = ("string", "Optional project id for project-specific schema.") })),
        ];
    }

    private static JsonObject Schema(
        Dictionary<string, (string Type, string Description)> properties,
        string[]? required = null)
    {
        var props = new JsonObject();
        foreach (var (name, (type, description)) in properties)
        {
            props[name] = new JsonObject { ["type"] = type, ["description"] = description };
        }

        var schema = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = props,
        };
        if (required is { Length: > 0 })
        {
            var requiredArray = new JsonArray();
            foreach (var name in required)
            {
                requiredArray.Add(name);
            }

            schema["required"] = requiredArray;
        }

        return schema;
    }
}
