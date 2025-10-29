# Zendesk MCP Server

A lightweight C# MCP (Model Context Protocol) server that provides access to Zendesk Help Center articles in Cursor AI.

## Features

- **Search Articles**: Search your Zendesk Help Center by keywords
- **Get Article**: Retrieve a specific article by ID
- **List Articles**: Browse all available articles

## Setup

### 1. Build the Server

```bash
cd ZendeskMcpServer
dotnet build --no-restore
```

### 2. Get Your Zendesk Credentials

You'll need:
- **Subdomain**: Your Zendesk subdomain (e.g., `yourcompany` from `yourcompany.zendesk.com`)
- **Email**: Your Zendesk account email
- **API Token**: Generate from Zendesk Admin > Apps and integrations > APIs > Zendesk API > Settings

### 3. Configure in Cursor

1. Open Cursor Settings (**Ctrl+Shift+J** on Windows)
2. Go to **MCP** tab → **Add MCP Server**
3. Add this configuration:

```json
{
  "mcpServers": {
    "zendesk": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "C:\\projects\\ZendeskMcpServer\\ZendeskMcpServer.csproj"
      ],
      "env": {
        "ZENDESK_SUBDOMAIN": "employmenthero1713155541",
        "ZENDESK_EMAIL": "steven.whitehead@webscale.com.au",
        "ZENDESK_API_TOKEN": "your-api-token"
      }
    }
  }
}
```

**Replace**:
- `your-subdomain` with your actual Zendesk subdomain
- `your-email@example.com` with your Zendesk email
- `your-api-token` with your API token

### 4. Restart Cursor

After adding the configuration, restart Cursor to load the MCP server.

## Usage in Cursor

Once configured, you can ask Cursor to search your Zendesk articles:

- "Search our Zendesk articles for payroll setup"
- "Get article 12345678 from Zendesk"
- "Show me recent Zendesk articles about tax filing"

The MCP server will automatically:
1. Connect to your Zendesk Help Center
2. Search/retrieve the articles
3. Return the content to Cursor AI

## Available Tools

### search_articles
Search articles by keyword.

**Parameters**:
- `query` (string, required): Search query
- `limit` (number, optional): Max results (default: 10)

### get_article
Get a specific article by ID.

**Parameters**:
- `article_id` (string, required): The article ID

### list_articles
List all articles.

**Parameters**:
- `limit` (number, optional): Max results (default: 30)

## Troubleshooting

### "Authentication failed"
- Verify your email and API token are correct
- Make sure you're using `/token:` format (email/token:api_token)

### "Server not responding"
- Check that the project builds successfully
- Verify the path in the Cursor config is correct
- Look at Cursor's MCP logs in the settings

### "No articles found"
- Verify your Zendesk subdomain is correct
- Check that articles are published (not draft)
- Try a broader search query

## Security Note

⚠️ **Keep your API token secure!** Don't commit it to version control. The token is stored in Cursor's settings file locally on your machine.

