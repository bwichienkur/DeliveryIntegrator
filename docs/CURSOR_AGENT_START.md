# Cursor Agent — Lead Delivery Platform (paste as first message)

Copy everything between `---BEGIN AGENT PROMPT---` and `---END AGENT PROMPT---` into a new **Cursor Cloud Agent** (or Agent) chat on this repository.

---

---BEGIN AGENT PROMPT---

## Mission

Implement the **Lead Delivery Platform** in this repository from scratch: ingest leads (HTTP + SQL Server poller), enrich via configurable pipelines (including **allowlisted stored procedures**), route by institution rules to versioned **delivery configurations**, deliver to **multiple pluggable outbound channels** (ship **HTTP** fully in v1; scaffold **Email** and **Sftp** executors without full production hardening), track **Success / PartialSuccess / Failed** per run, expose a **React** admin UI with a **field catalog field-picker**, and support **retries** and **failure reporting by institution**.

**Authoritative requirements:** Read **`docs/lead-delivery-platform-build-prompt.md` end-to-end before writing code.** Treat it as the product spec. If anything is ambiguous, choose a reasonable default, document it in `docs/adr/`, and continue—do not block on questions unless you cannot proceed safely.

## Non-negotiable stack

| Area | Choice |
|------|--------|
| Backend | **.NET 8+**, ASP.NET Core |
| Data | **SQL Server**, **EF Core** (migrations in repo) |
| Admin UI | **React** + **TypeScript** (SPA in repo, not Blazor) |
| Jobs | Hangfire **or** Quartz + **transactional outbox** pattern |
| HTTP outbound | `IHttpClientFactory`, Polly (timeouts/retries per endpoint) |
| Rules | RulesEngine **or** tested rule DSL |
| Templates | **Cottle** for body/fragment transforms |
| Logs/traces | Serilog + OpenTelemetry hooks |

**Do not** substitute PostgreSQL, Blazor, or Angular. **Do not** let operators run arbitrary SQL or C# from the UI—only allowlisted SPs, mapped parameters via field catalog, Cottle, and registered enrichers.

## Architecture you must implement

1. **LeadDocument** — Normalized JSON on each lead; relational columns projected into it on ingest; raw payload retained.
2. **Field catalog** — `FieldCatalogEntries` + inference/pending approval; API for React **searchable tree picker**; sync from EF model + inferred JSON paths; enrichment outputs under `enriched.{stepKey}.*`.
3. **Enrichment pipeline** — Ordered steps (lookup, **stored procedure** with allowlist + parameter map from catalog, Cottle fragment, plugin enricher stub); snapshot **`EnrichedLeadSnapshot`** on each `DeliveryRun` for retries.
4. **Delivery configuration** — Institution-scoped, draft/publish versions, priority-ordered **rule matching** (campus, program, source, AND/OR groups).
5. **Delivery endpoints** — `ChannelType` + JSON config; HTTP: JSON/XML/form/multipart, auth (Basic, Bearer, OAuth client credentials with token cache), configurable success status codes, **Primary/Secondary/Optional** priority.
6. **Orchestration** — `IDeliveryChannelExecutor`; compute aggregate status (default ADR: **Failed if primary fails** even if secondaries succeed; **PartialSuccess** when primary succeeds and a required secondary fails).
7. **Delivery attempts** — Store redacted request/response, correlation IDs, link to timeline API for React.
8. **Outbox worker** — Process deliveries, retries with backoff, idempotent ingestion (`Idempotency-Key`).

## Repository layout (create this)

```
src/
  LeadDelivery.Api/
  LeadDelivery.Worker/
  LeadDelivery.Domain/
  LeadDelivery.Application/
  LeadDelivery.Infrastructure/
  lead-delivery-admin/          # React + Vite (or equivalent)
tests/
  LeadDelivery.UnitTests/
  LeadDelivery.IntegrationTests/ # Testcontainers SQL Server + WireMock
docs/
  adr/                            # ADRs for open decisions
docker-compose.yml                # SQL Server, API, worker, admin (dev)
```

## Phased execution (follow this order)

**Phase 0 — Bootstrap**  
Solution, EF Core + initial migration, Docker Compose SQL Server, health endpoints, README with local run steps.

**Phase 1 — Ingestion + domain**  
Leads API (`POST /api/v1/leads` → 202), Lead entity, LeadDocument normalization, idempotency, structured logging.

**Phase 2 — Field catalog**  
Tables, metadata sync from EF model, JSON path inference on ingest, GET field-catalog (search/tree), POST refresh with pending inference approval API (admin can mark approved—even if minimal UI at first).

**Phase 3 — Routing + configs**  
Institutions, delivery configs (versioned publish), RulesEngine integration, simulate/match API.

**Phase 4 — Enrichment**  
Pipeline model, SP executor (allowlist table, parameter mapping), simulate/enrichment API, snapshot on run.

**Phase 5 — HTTP channel + orchestration**  
Cottle + field maps + lookup tables, OAuth client credentials handler, multi-endpoint runs, attempt persistence, outbox + Hangfire/Quartz processing.

**Phase 6 — React admin (MVP)**  
Institutions, config editor with **field picker** on mappings, rule test/simulate, runs list + **timeline**, retry failed endpoints, failures list filterable by institution (export CSV optional but valued).

**Phase 7 — Channel scaffolding**  
`EmailDeliveryExecutor` and `SftpDeliveryExecutor` registered in DI with config schema validation and **NotImplemented** or stub success in dev unless quick win is easy—**orchestration must not change** when adding them.

**Phase 8 — Quality**  
Unit tests (rules, aggregate status, transforms); integration test: ingest → match → enrich → WireMock HTTP → PartialSuccess → retry.

Write ADRs in Phase 0/1 for: Hangfire vs Quartz, JSON storage (`NVARCHAR(MAX)` + `OPENJSON` is fine), primary-failure semantics, secondary execution when primary fails (default: **do not send secondaries if primary fails** unless config flag says otherwise—implement the flag).

## API minimum (implement)

All paths from the build prompt’s **API surface** section—at least stub admin CRUD where UI not ready yet, but ingestion, catalog, simulate, runs, retry must be functional.

## Security baseline

- Secrets as configuration/Key Vault placeholders—never commit real secrets.
- API key or JWT for admin routes (simple dev auth acceptable; document production gap).
- PII fields flagged in catalog; redact in attempt logs and non-admin roles.

## Definition of done (verify before finishing)

Check off against **`docs/lead-delivery-platform-build-prompt.md` → Acceptance criteria** (items 1–10 minimum; item 11 load test can be a documented manual script).

Additionally:

- [ ] `docker compose up` brings SQL Server + API + worker; README documents steps.
- [ ] Seed data: 2 institutions, 1 config, 2 HTTP endpoints (WireMock URL), sample field catalog, 1 allowlisted SP (in seed script or migration SQL).
- [ ] React app builds and demos field picker wired to real catalog API.
- [ ] PR with summary, ADR links, and how to test E2E locally.

## How to work

- Create a feature branch, commit incrementally, open a PR when MVP is runnable.
- Prefer small, reviewable commits per phase.
- When the spec and implementation diverge, **update ADR—not the spec** unless you fixed an error in the spec (then edit the build prompt and note why).

**Start now:** Phase 0 — read `docs/lead-delivery-platform-build-prompt.md`, then scaffold the solution and Docker Compose.

---END AGENT PROMPT---