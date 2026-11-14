# Lead Delivery Platform

.NET 8 + SQL Server + EF Core + React admin for configurable lead routing, enrichment, and multi-endpoint HTTP delivery.

## Documentation

- [Product spec](docs/lead-delivery-platform-build-prompt.md)
- [ADRs](docs/adr/)
- [Cursor agent kickoff](docs/CURSOR_AGENT_START.md)

## Prerequisites

- Docker Desktop (SQL Server, API, worker, WireMock, admin UI)
- .NET 8 SDK (local dev without Docker builds)

## Quick start (Docker)

```bash
docker compose up --build
```

| Service | URL |
|---------|-----|
| API | http://localhost:5080/swagger |
| Admin UI | http://localhost:5173 |
| WireMock | http://localhost:8080 |

### Ingest a sample lead

```bash
curl -s -X POST http://localhost:5080/api/v1/leads \
  -H 'Content-Type: application/json' \
  -H 'Idempotency-Key: demo-1' \
  -d '{
    "institutionCode": "CONTOSOU",
    "payload": {
      "email": "lead@example.com",
      "campus": "Online",
      "program": "MBA",
      "sourceSite": "WebsiteA"
    }
  }'
```

The worker processes the outbox within about a minute (Hangfire recurring job). Check runs:

```bash
curl -s "http://localhost:5080/api/v1/delivery-runs?institutionId=11111111-1111-1111-1111-111111111111"
```

### Admin API key

Protected routes accept header `X-Admin-Api-Key: dev-admin-key` (override with `ADMIN_API_KEY`).

### Stored procedure seed

Apply `database/001_stored_procedures.sql` to SQL Server (mount or run manually). Procedure `ld.ld_GetProgramDetails` is allowlisted in seed data.

## Local development (no Docker)

1. Start SQL Server and set `ConnectionStrings:LeadDelivery` in `src/LeadDelivery.Api/appsettings.Development.json`.
2. `dotnet run --project src/LeadDelivery.Api`
3. `dotnet run --project src/LeadDelivery.Worker`
4. `cd src/lead-delivery-admin && npm install && npm run dev` (set `VITE_API_BASE` to API URL)

## Tests

```bash
export DOTNET_ROOT=$HOME/.dotnet
export PATH=$HOME/.dotnet:$PATH
dotnet test
```

## E2E notes

Seed includes Contoso **Online MBA** config with two HTTP endpoints targeting WireMock paths `/crm` and `/legacy`. Configure WireMock stubs or expect partial/failed delivery until stubs return 200/201.

Retry failed endpoints:

```bash
curl -X POST "http://localhost:5080/api/v1/delivery-runs/{runId}/retry" \
  -H 'X-Admin-Api-Key: dev-admin-key' \
  -H 'Content-Type: application/json' \
  -d '{"endpointIds":[]}'
```

## Solution layout

See [build prompt](docs/lead-delivery-platform-build-prompt.md#solution-structure).