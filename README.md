# Trading Signals API

ASP.NET Core 8.0 Web API application for handling trading signals via webhooks.

## Features

- Receive trading signals via custom webhooks
- Secure webhook endpoints with secret verification
- Retrieve pending signals for MT5 integration
- Manage webhook configurations
- API key authentication for sensitive endpoints
- SQLite database for data persistence

## API Endpoints

### Webhook Endpoint

```
POST /webhook/{path}
```

Receives trading signals from external sources. The `path` is a custom identifier that must match a configured webhook path.

**Payload Example**:
```json
{
  "secret": "your-webhook-secret",
  "symbol": "EURUSD",
  "action": "BUY",
  "price": 1.05432,
  "timestamp": "2025-08-08T10:30:00Z",
  "message": "Strong bullish signal"
}
```

### Pending Signals Endpoint

```
GET /signals/pending
```

Retrieves all pending trading signals and marks them as processed. Requires API key authentication via `ApiKey` header or `apiKey` query parameter.

### Webhook Configuration Endpoints

```
GET /config/webhooks
POST /config/webhooks
DELETE /config/webhooks/{id}
```

Manage webhook configurations. All endpoints require authentication via `ConfigApiKey` header.

## Configuration

API keys can be configured via:
- Environment variables (`API_KEY` and `CONFIG_API_KEY`)
- Application settings in `appsettings.json`

## Database

The application uses SQLite database (signals.db) with Entity Framework Core.

## Running the Application

### Local Development

```bash
dotnet run
```

### Docker

```bash
# Build the Docker image
docker build -t trading-signals-api .

# Run the container
docker run -p 8080:80 -e API_KEY=your-api-key -e CONFIG_API_KEY=your-config-api-key trading-signals-api
```

## Swagger Documentation

Swagger UI is available at `/swagger` when running in Development environment.
