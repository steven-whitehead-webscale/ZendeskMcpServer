using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZendeskMcpServer;

public class ZendeskClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public ZendeskClient(string subdomain, string email, string apiToken)
    {
        _baseUrl = $"https://{subdomain}.zendesk.com";
        _httpClient = new HttpClient();
        
        // Basic auth: email/token:api_token
        var authString = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{email}/token:{apiToken}")
        );
        _httpClient.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authString);
    }

    public async Task<List<ArticleResult>> SearchArticlesAsync(string query, int limit = 10)
    {
        var url = $"{_baseUrl}/api/v2/help_center/articles/search.json?query={Uri.EscapeDataString(query)}&per_page={limit}";
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var searchResponse = JsonSerializer.Deserialize<ArticleSearchResponse>(json, new JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true 
        });

        return searchResponse?.Results ?? new List<ArticleResult>();
    }

    public async Task<Article> GetArticleAsync(string articleId)
    {
        var url = $"{_baseUrl}/api/v2/help_center/articles/{articleId}.json";
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var articleResponse = JsonSerializer.Deserialize<ArticleResponse>(json, new JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true 
        });

        return articleResponse?.Article ?? throw new Exception("Article not found");
    }

    public async Task<List<Article>> ListArticlesAsync(int limit = 30)
    {
        var url = $"{_baseUrl}/api/v2/help_center/articles.json?per_page={limit}";
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var listResponse = JsonSerializer.Deserialize<ArticleListResponse>(json, new JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true 
        });

        return listResponse?.Articles ?? new List<Article>();
    }
}

// Zendesk API Response Models
public class ArticleSearchResponse
{
    [JsonPropertyName("results")]
    public List<ArticleResult> Results { get; set; } = new();
}

public class ArticleResult
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("body")]
    public string Body { get; set; } = "";

    [JsonPropertyName("url")]
    public string Url { get; set; } = "";

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = "";

    [JsonPropertyName("snippet")]
    public string? Snippet { get; set; }

    [JsonPropertyName("author_id")]
    public long? AuthorId { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}

public class ArticleResponse
{
    [JsonPropertyName("article")]
    public Article Article { get; set; } = new();
}

public class ArticleListResponse
{
    [JsonPropertyName("articles")]
    public List<Article> Articles { get; set; } = new();
}

public class Article
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("body")]
    public string Body { get; set; } = "";

    [JsonPropertyName("url")]
    public string Url { get; set; } = "";

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = "";

    [JsonPropertyName("author_id")]
    public long? AuthorId { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [JsonPropertyName("draft")]
    public bool Draft { get; set; }

    [JsonPropertyName("promoted")]
    public bool Promoted { get; set; }

    [JsonPropertyName("position")]
    public int Position { get; set; }

    [JsonPropertyName("section_id")]
    public long? SectionId { get; set; }
}

