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

The server supports two modes: **stdio mode** (default) and **HTTP server mode**. Choose the one that fits your needs.

#### Option A: Stdio Mode (Default)

This mode runs the server as a process that Cursor launches directly. Each user needs to configure their own credentials.

**Note**: You can also create an `appsettings.json` file with your credentials, and the server will use them if environment variables aren't set. However, for stdio mode, it's typically easier to configure credentials directly in Cursor's MCP settings.

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

#### Option B: HTTP Server Mode

This mode runs the server as a web service that multiple users can connect to. The server itself has the credentials configured, so users only need to specify the URL.

**Server Setup:**

1. Run the server in HTTP mode:

```bash
dotnet run --project ZendeskMcpServer.csproj -- --http
```

Or set the environment variable:

```bash
export MCP_SERVER_MODE=http
dotnet run --project ZendeskMcpServer.csproj
```

2. Configure the server with your Zendesk credentials. You can use either a config file or environment variables:

**Option 1: Using a config file (Recommended)**

Create an `appsettings.json` file in the project directory:

```json
{
  "Zendesk": {
    "Subdomain": "your-subdomain",
    "Email": "your-email@example.com",
    "ApiToken": "your-api-token"
  },
  "Server": {
    "Port": "8080",
    "Url": "http://0.0.0.0:8080"
  }
}
```

Copy `appsettings.json.example` to `appsettings.json` and fill in your values:

```bash
cp appsettings.json.example appsettings.json
# Edit appsettings.json with your credentials
```

**Option 2: Using environment variables**

```bash
export ZENDESK_SUBDOMAIN="your-subdomain"
export ZENDESK_EMAIL="your-email@example.com"
export ZENDESK_API_TOKEN="your-api-token"
export PORT="8080"  # Optional, defaults to 8080
```

**Note**: Environment variables take precedence over config file values if both are set.

3. The server will start on `http://0.0.0.0:8080` (or your specified port)

**Client Configuration:**

In Cursor, configure the MCP server to connect via HTTP:

```json
{
  "mcpServers": {
    "zendesk": {
      "type": "http",
      "url": "https://zendeskmcpserver/"
    }
  }
}
```

**Note**: Replace `https://zendeskmcpserver/` with your actual server URL. The server must be accessible from where Cursor is running.

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

## Running as HTTP Server

The server can run as an HTTP service, which is useful for:
- **Centralized deployment**: One server instance for multiple users
- **Simplified client config**: Users only need the server URL
- **Production deployments**: Can be deployed to cloud services, Docker containers, etc.

### Starting the HTTP Server

```bash
# Using command-line argument
dotnet run --project ZendeskMcpServer.csproj -- --http

# Or using environment variable
export MCP_SERVER_MODE=http
dotnet run --project ZendeskMcpServer.csproj
```

### Configuration

The server reads configuration from `appsettings.json` or environment variables. Environment variables take precedence over config file values.

**Config File (`appsettings.json`):**

```json
{
  "Zendesk": {
    "Subdomain": "your-subdomain",
    "Email": "your-email@example.com",
    "ApiToken": "your-api-token"
  },
  "Server": {
    "Port": "8080",
    "Url": "http://0.0.0.0:8080"
  }
}
```

**Environment Variables:**

- `ZENDESK_SUBDOMAIN` - Your Zendesk subdomain (required)
- `ZENDESK_EMAIL` - Your Zendesk account email (required)
- `ZENDESK_API_TOKEN` - Your Zendesk API token (required)
- `PORT` - Server port (optional, defaults to 8080)
- `MCP_SERVER_URL` - Full server URL (optional, defaults to `http://0.0.0.0:{PORT}`)

**Priority Order:**
1. Environment variables (highest priority)
2. `appsettings.json` config file
3. Default values

### Endpoints

- `POST /` - Main MCP protocol endpoint (accepts JSON-RPC requests)
- `GET /health` - Health check endpoint

### Example: Running in Docker

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["ZendeskMcpServer.csproj", "./"]
RUN dotnet restore
COPY . .
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV MCP_SERVER_MODE=http
ENV PORT=8080
ENTRYPOINT ["dotnet", "ZendeskMcpServer.dll", "--http"]
```

## Security Note

⚠️ **Keep your API token secure!** Don't commit it to version control. 

- **Stdio mode**: The token is stored in Cursor's settings file locally on your machine.
- **HTTP mode**: The token is stored as an environment variable on the server. Use secure secret management practices (environment variables, secret managers, etc.) when deploying.

