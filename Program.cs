using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZendeskMcpServer;

class Program
{
    static async Task Main(string[] args)
    {
        var server = new McpServer();
        await server.RunAsync();
    }
}

// MCP Protocol Classes
public record McpRequest(
    [property: JsonPropertyName("jsonrpc")] string JsonRpc,
    [property: JsonPropertyName("id")] JsonElement? Id,
    [property: JsonPropertyName("method")] string Method,
    [property: JsonPropertyName("params")] JsonElement? Params
);

public record McpResponse(
    [property: JsonPropertyName("jsonrpc")] string JsonRpc,
    [property: JsonPropertyName("id")] JsonElement? Id,
    [property: JsonPropertyName("result")] object? Result,
    [property: JsonPropertyName("error")] McpError? Error
);

public record McpError(
    [property: JsonPropertyName("code")] int Code,
    [property: JsonPropertyName("message")] string Message
);

// MCP Server Implementation
public class McpServer
{
    private readonly ZendeskClient _zendeskClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public McpServer()
    {
        var subdomain = Environment.GetEnvironmentVariable("ZENDESK_SUBDOMAIN") ?? "";
        var email = Environment.GetEnvironmentVariable("ZENDESK_EMAIL") ?? "";
        var apiToken = Environment.GetEnvironmentVariable("ZENDESK_API_TOKEN") ?? "";

        _zendeskClient = new ZendeskClient(subdomain, email, apiToken);
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    public async Task RunAsync()
    {
        var stdin = Console.OpenStandardInput();
        var reader = new StreamReader(stdin);

        while (true)
        {
            var line = await reader.ReadLineAsync();
            if (line == null) break;
            if (string.IsNullOrWhiteSpace(line)) continue;

            await ProcessRequestAsync(line);
        }
    }

    private async Task ProcessRequestAsync(string requestJson)
    {
        try
        {
            var request = JsonSerializer.Deserialize<McpRequest>(requestJson, _jsonOptions);
            if (request == null) return;

            object? result = request.Method switch
            {
                "initialize" => HandleInitialize(),
                "tools/list" => HandleToolsList(),
                "tools/call" => await HandleToolsCallAsync(request.Params),
                _ => null
            };

            var response = new McpResponse("2.0", request.Id, result, null);
            WriteResponse(response);
        }
        catch (Exception ex)
        {
            var errorResponse = new McpResponse(
                "2.0",
                null,
                null,
                new McpError(-32603, $"Internal error: {ex.Message}")
            );
            WriteResponse(errorResponse);
        }
    }

    private object HandleInitialize()
    {
        return new
        {
            protocolVersion = "2024-11-05",
            capabilities = new
            {
                tools = new { }
            },
            serverInfo = new
            {
                name = "zendesk-mcp-server",
                version = "1.0.0"
            }
        };
    }

    private object HandleToolsList()
    {
        return new
        {
            tools = new object[]
            {
                new
                {
                    name = "search_articles",
                    description = "Search Zendesk Help Center articles by query string",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            query = new
                            {
                                type = "string",
                                description = "Search query for articles"
                            },
                            limit = new
                            {
                                type = "number",
                                description = "Maximum number of results (default: 10)"
                            }
                        },
                        required = new[] { "query" }
                    }
                },
                new
                {
                    name = "get_article",
                    description = "Get a specific Zendesk article by ID",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            article_id = new
                            {
                                type = "string",
                                description = "The article ID"
                            }
                        },
                        required = new[] { "article_id" }
                    }
                },
                new
                {
                    name = "list_articles",
                    description = "List all articles in the Help Center",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            limit = new
                            {
                                type = "number",
                                description = "Maximum number of results (default: 30)"
                            }
                        }
                    }
                }
            }
        };
    }

    private async Task<object> HandleToolsCallAsync(JsonElement? paramsElement)
    {
        if (!paramsElement.HasValue)
        {
            throw new Exception("Missing params");
        }

        var toolName = paramsElement.Value.GetProperty("name").GetString();
        var arguments = paramsElement.Value.TryGetProperty("arguments", out var args) 
            ? args 
            : default;

        var content = toolName switch
        {
            "search_articles" => await SearchArticlesAsync(arguments),
            "get_article" => await GetArticleAsync(arguments),
            "list_articles" => await ListArticlesAsync(arguments),
            _ => throw new Exception($"Unknown tool: {toolName}")
        };

        return new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text = content
                }
            }
        };
    }

    private async Task<string> SearchArticlesAsync(JsonElement arguments)
    {
        var query = arguments.GetProperty("query").GetString() ?? "";
        var limit = arguments.TryGetProperty("limit", out var limitProp) 
            ? limitProp.GetInt32() 
            : 10;

        var results = await _zendeskClient.SearchArticlesAsync(query, limit);
        return JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
    }

    private async Task<string> GetArticleAsync(JsonElement arguments)
    {
        var articleId = arguments.GetProperty("article_id").GetString() ?? "";
        var article = await _zendeskClient.GetArticleAsync(articleId);
        return JsonSerializer.Serialize(article, new JsonSerializerOptions { WriteIndented = true });
    }

    private async Task<string> ListArticlesAsync(JsonElement arguments)
    {
        var limit = arguments.TryGetProperty("limit", out var limitProp) 
            ? limitProp.GetInt32() 
            : 30;

        var results = await _zendeskClient.ListArticlesAsync(limit);
        return JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
    }

    private void WriteResponse(McpResponse response)
    {
        var json = JsonSerializer.Serialize(response, _jsonOptions);
        Console.WriteLine(json);
    }
}
