using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace ZendeskMcpServer;

class Program
{
    static async Task Main(string[] args)
    {
        // Check if we should run as HTTP server
        var httpMode = args.Contains("--http") || 
                       Environment.GetEnvironmentVariable("MCP_SERVER_MODE")?.ToLower() == "http";

        if (httpMode)
        {
            await RunHttpServerAsync();
        }
        else
        {
            await RunStdioServerAsync();
        }
    }

    static async Task RunHttpServerAsync()
    {
        var builder = WebApplication.CreateBuilder();
        
        // Add configuration from appsettings.json and environment variables
        builder.Configuration
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();
        
        // Add memory cache for Zendesk API response caching
        builder.Services.AddMemoryCache();
        
        var app = builder.Build();
        
        // Get IMemoryCache from service provider
        var cache = app.Services.GetRequiredService<IMemoryCache>();
        var mcpServer = new McpServer(builder.Configuration, cache);

        // MCP protocol endpoint
        app.MapPost("/", async (HttpContext context) =>
        {
            using var reader = new StreamReader(context.Request.Body);
            var requestJson = await reader.ReadToEndAsync();
            
            if (string.IsNullOrWhiteSpace(requestJson))
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync("Empty request body");
                return;
            }

            var response = await mcpServer.ProcessRequestAsync(requestJson);
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(response);
        });

        // Health check endpoint
        app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

        // Read port and URL from config or environment variables
        var port = builder.Configuration["Server:Port"] 
                ?? Environment.GetEnvironmentVariable("PORT") 
                ?? "8080";
        var url = builder.Configuration["Server:Url"] 
               ?? Environment.GetEnvironmentVariable("MCP_SERVER_URL") 
               ?? $"http://0.0.0.0:{port}";
        
        app.Urls.Add(url);
        
        Console.WriteLine($"Zendesk MCP Server running in HTTP mode on {url}");
        await app.RunAsync();
    }

    static async Task RunStdioServerAsync()
    {
        // Build configuration from appsettings.json and environment variables
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();
        
        // Create memory cache for stdio mode
        var cache = new MemoryCache(new MemoryCacheOptions());
        
        var server = new McpServer(configuration, cache);
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

    public McpServer(IConfiguration? configuration = null, IMemoryCache? cache = null)
    {
        // Read from configuration (appsettings.json) or environment variables
        // Environment variables take precedence over config file
        var subdomain = configuration?["Zendesk:Subdomain"] 
                      ?? Environment.GetEnvironmentVariable("ZENDESK_SUBDOMAIN") 
                      ?? configuration?["ZENDESK_SUBDOMAIN"] 
                      ?? "";
        
        var email = configuration?["Zendesk:Email"] 
                  ?? Environment.GetEnvironmentVariable("ZENDESK_EMAIL") 
                  ?? configuration?["ZENDESK_EMAIL"] 
                  ?? "";
        
        var apiToken = configuration?["Zendesk:ApiToken"] 
                     ?? Environment.GetEnvironmentVariable("ZENDESK_API_TOKEN") 
                     ?? configuration?["ZENDESK_API_TOKEN"] 
                     ?? "";

        _zendeskClient = new ZendeskClient(subdomain, email, apiToken, cache);
        
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

            await ProcessRequestStdioAsync(line);
        }
    }

    public async Task<string> ProcessRequestAsync(string requestJson)
    {
        try
        {
            var request = JsonSerializer.Deserialize<McpRequest>(requestJson, _jsonOptions);
            if (request == null) 
            {
                var nullResponse = new McpResponse("2.0", null, null, new McpError(-32600, "Invalid request"));
                return JsonSerializer.Serialize(nullResponse, _jsonOptions);
            }

            object? result = request.Method switch
            {
                "initialize" => HandleInitialize(),
                "tools/list" => HandleToolsList(),
                "tools/call" => await HandleToolsCallAsync(request.Params),
                _ => null
            };

            var response = new McpResponse("2.0", request.Id, result, null);
            return JsonSerializer.Serialize(response, _jsonOptions);
        }
        catch (Exception ex)
        {
            var errorResponse = new McpResponse(
                "2.0",
                null,
                null,
                new McpError(-32603, $"Internal error: {ex.Message}")
            );
            return JsonSerializer.Serialize(errorResponse, _jsonOptions);
        }
    }

    private async Task ProcessRequestStdioAsync(string requestJson)
    {
        var responseJson = await ProcessRequestAsync(requestJson);
        Console.WriteLine(responseJson);
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

}
