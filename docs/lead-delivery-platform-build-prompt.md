# Build Prompt: .NET Lead Delivery Platform

Configurable multi-endpoint routing, field catalog, enrichment, and pluggable outbound channels.

---

## Product summary

Build an enterprise **Lead Delivery Platform** that:

1. **Ingests** lead data from **HTTP requests** (synchronous or webhook-style) and/or **polling/reading from SQL Server** (and optional message queues).
2. **Enriches** leads through a configurable pipeline (lookups, allowlisted stored procedures, templates).
3. **Selects** a **Delivery Configuration** using institution-aware rules (campus, program, source site, custom attributes).
4. **Delivers** the lead to **one or more external targets** per configuration via a **channel plugin model** (HTTP first; SFTP and email later), with per-endpoint auth, payload format, field mapping/transformations, and success criteria.
5. **Tracks** per-lead and per-endpoint outcomes (full success, partial success, failure), supports **retries**, and provides a **React admin UI** plus **operational dashboards** (failures by institution, endpoint, time range).

**Primary stack:**

| Layer | Choice |
|--------|--------|
| Runtime | **.NET 8+** (LTS) |
| APIs & workers | **ASP.NET Core** |
| Admin UI | **React** (TypeScript) + REST admin APIs |
| Persistence | **SQL Server** + **EF Core** |
| Background jobs | **Hangfire** or **Quartz.NET** + transactional outbox |
| Outbound HTTP | `IHttpClientFactory` + **Polly** |
| Rules routing | **RulesEngine** or embedded DSL with unit tests |
| Transformations | **Cottle** + DB-backed lookup maps |
| Secrets | Azure Key Vault / equivalent; secret references in DB, never plaintext in UI |
| Observability | **Serilog** + **OpenTelemetry** (logs, traces, metrics) |

Optimize for: **reliability**, **observability (easy debugging)**, **safe configuration changes**, **throughput**, and **operator ergonomics** (retries, filters, exports).

Avoid arbitrary C# in the database editable from the UI without sandboxing; prefer **Cottle** and **approved built-in/enricher functions** for complex logic.

---

## Core domain concepts

1. **Lead** — Canonical record (raw payload + normalized `LeadDocument` + institution/campus/program metadata + source).
2. **Institution** — Tenant boundary; configs, endpoints, field catalog, and failure views are scoped by institution.
3. **Field Catalog** — Discoverable mappable paths for pickers and templates (columns, JSON/XML paths, form imports, enrichment outputs).
4. **Delivery Configuration** — Named, versioned ruleset: when it applies + ordered list of **Delivery Endpoints** + overall success semantics + optional enrichment pipeline override.
5. **Delivery Endpoint** — Single outbound target (channel-specific config): URL/mailbox/path, method, content type, auth profile, transforms, success rules, priority, retry policy.
6. **Delivery Attempt** — One execution of one endpoint for one lead (redacted request/response snapshot, status, duration, correlation ID).
7. **Delivery Run** — Aggregate result for a lead under one configuration (computed status).

### Lead delivery aggregate status (required)

- **Success** — All required endpoints succeeded per their rules.
- **PartialSuccess** — At least one success and at least one failure among endpoints that count toward completion.
- **Failed** — Primary-required endpoints failed, or all endpoints failed per configured rules.
- **Pending / InProgress** — Queued or in flight.
- **Cancelled** — Operator or system cancelled before completion.

Document in ADR how **primary endpoint** affects aggregate status (e.g. run is **Failed** if primary fails even when secondaries succeed).

---

## Canonical lead model + field catalog (schema registry)

### Principle

Separate **storage** from **what operators can bind to**. Every mappable path is an entry in the **Field Catalog** (institution-scoped, with optional shared templates).

### Sources that feed the catalog

| Source type | How fields appear | Refresh |
|-------------|-------------------|---------|
| **SQL Server columns** | EF model / metadata sync job → `lead.email`, `lead.campusId` | On deploy, migration hook, or scheduled sync |
| **JSON on lead** | Infer schema from payloads → normalized paths in `LeadDocument` | On new paths + optional admin approval |
| **XML on lead** | XPath registry; optional XSD upload | Sample-based |
| **Form definitions** | Import field manifest when form is published | Event/webhook |
| **Enrichment pipeline** | Outputs as `enriched.{stepKey}.{fieldName}` | When step is published |
| **Manual** | Admin-defined fields for labels/constants | Immediate |

### Normalization (recommended)

- Persist **raw** body + **normalized `LeadDocument`** (JSON in SQL Server, e.g. `NVARCHAR(MAX)` with JSON functions or native JSON type where appropriate).
- Relational columns are **projected into** `LeadDocument` on ingest so the catalog exposes a single tree for UI and Cottle context (`lead.*`).

### Catalog entry shape

- `key` — stable id (`lead.program.code`)
- `displayName`, `dataType`, `path` (JSONPath/XPath/column)
- `source` — Column | JsonInferred | XmlInferred | FormImport | Enrichment | Manual
- `sampleValues` (redacted), `institutionId`, `isPii`, `deprecatedAt`

### SQL Server tables (additions)

- `FieldCatalogEntries`
- `FieldCatalogInferences` (pending paths awaiting approval)
- `FormFieldManifests`
- Index: `(InstitutionId, Key)`; full-text or indexed `DisplayName` for React picker search.

---

## UX: React field picker for delivery configuration

### Requirements

- **Field Picker** on map rows and template editors:
  - Searchable dropdown + tree for nested JSON
  - Type icons, PII badges, recently used / pinned
  - Drag or click to set source field; **Insert field** for Cottle (`{{ lead.x.y }}`)
- **Autocomplete** in template editor on `{{ lead.`
- **Test panel**: sample or real lead with resolved values (redact PII by role)
- **Refresh field list**: re-scan SQL metadata + merge inferred JSON paths; **diff view** (approve/ignore new fields)
- **Form publish hook**: register new form fields automatically
- Default: no raw JSONPath unless **advanced** permission

### Acceptance (field UX)

- New form field landing in `LeadDocument` appears in picker after ingest auto-infer or refresh.
- New `Lead` table column appears after EF migration + metadata sync without redeploying React (API returns updated catalog).

---

## Ingestion

### Sources

1. **HTTP ingestion API** — JSON (default), optional XML; validate; store raw + normalized; **202 Accepted** with `leadId`.
2. **SQL Server ingestion** — Pollers on `UpdatedAt`/status column or dedicated outbox/queue table; idempotent dedupe.
3. **Optional message bus** — Same processing pipeline.

### Idempotency

- `Idempotency-Key` header or natural keys; return same `deliveryRunId` on replay.

---

## Configuration routing (UI + engine)

1. Resolve **Institution** (payload, API key, subdomain, lookup).
2. Evaluate **published** Delivery Configurations in priority order.
3. Criteria: campus, program, source site, geography, custom tags, lead fields (equals, in-list, regex, exists, ranges, nested AND/OR).
4. **React rule builder**: priority, test with sample JSON, simulation (no send).
5. **Versioning**: draft → publish → rollback; effective dates optional.

---

## Dynamic enrichment & database access (stored procedures + more)

### Enrichment pipeline (before outbound transforms)

Ordered **Enrichment Steps** per institution or per delivery config:

| Step type | Purpose |
|-----------|---------|
| Catalog / lookup | Parameterized table or key-value query |
| **Stored procedure** | Legacy/custom SQL logic via allowlist |
| Template expression | Cottle fragment merging JSON into `lead` |
| HTTP internal API | Optional internal services |
| Built-in / plugin enrichers | .NET `IDataEnricher` with JSON config schema |

Each step: `stepKey`, order, institution, timeout, enabled, version; outputs merged into `lead.enriched.{stepKey}` and **registered in Field Catalog**.

### Stored procedure connector (migration-friendly)

- **Procedure Command** in admin:
  - Name from **allowlist** (e.g. schema prefix `ld_`; sync from `sys.procedures`)
  - Parameter map via field picker → `SqlParameter` (no UI-built dynamic SQL)
  - Result mapping: single row, multi-row array, or output parameters
- `CommandType.StoredProcedure`, timeouts, read-only connection where possible.
- DBAs deploy procedures; platform only references allowlisted names.

### More dynamic long-term

- Parameterized **saved queries** (super-admin only)
- **Integration views** owned by DB team
- **C# enricher plugins** for complex logic
- Rules + lookup tables to retire SPs over time

### Enrichment UI (React)

- Pipeline builder, drag-reorder, test with sample lead (timing + merged document).
- Errors on delivery timeline: `EnrichmentStep 'sp_Program' timed out`.

### Safety & retries

- Per-step timeout, circuit breaker, optional cache (TTL) keyed by `(procedure, param hash)`.
- Snapshot **`EnrichedLeadSnapshot`** on `DeliveryRun` for consistent retries; document SP idempotency (read-only SPs preferred).

### SQL Server tables

- `EnrichmentSteps` (JSON config)
- `AllowedStoredProcedures`

---

## Delivery endpoints & channels

### Pluggable channels

```
IDeliveryChannelExecutor → ExecuteAsync(DeliveryContext) → DeliveryAttemptResult
```

| Phase | Channel | Notes |
|-------|---------|--------|
| 1 | **Http** | POST/PUT/PATCH/DELETE; JSON, XML, form, multipart |
| 2 | **Email** | SMTP/SendGrid/Microsoft Graph; HTML/text from Cottle; attachments |
| 3 | **Sftp** | Path/filename templates; key/password from vault; payload = rendered file |

Shared across channels: retries, primary/secondary, audit timeline, institution-scoped failures.

### HTTP endpoint configuration

- Method, URL (templated), headers, timeout
- Body formats: JSON, XML, `application/x-www-form-urlencoded`, multipart, raw
- Auth: None, Basic, Bearer, OAuth 2.0 (client credentials, refresh), API key
- Success: HTTP status lists/ranges + optional JSONPath/body regex
- Priority: **Primary** / **Secondary** / **Optional**
- Ordering: serial vs parallel; stop-on-primary-failure (configurable)
- Retry: max attempts, backoff, retryable statuses/timeouts/5xx

### Email / SFTP (later phases)

- Same field picker and Cottle templates
- Channel-specific success criteria (message id, upload ack, file size check)
- SFTP preview: filename + truncated file content in admin

### Outbound execution

- Named `HttpClient` per endpoint; delegating handlers (logging, auth, Polly)
- Redacted bodies on `DeliveryAttempt`; correlation / W3C trace propagation

### Endpoint model in SQL Server

- `DeliveryEndpoints.ChannelType` + validated `ChannelConfig` (JSON)
- EF Core owns migrations for relational core; JSON columns validated per channel schema at publish time

---

## Field mapping & transformations

1. **Simple maps** — React grid: catalog source → target; value lookup tables (`State=NY` → `US-NY`).
2. **Expressions** — Safe string/date/concat/default (no I/O).
3. **Cottle templates** — Full body or sections; context: `lead`, `institution`, `endpoint`, `utcNow`, helpers.

- Reusable transformation library per institution
- Template lint + preview against sample leads
- Invoke only **registered functions** from Cottle (not user assemblies)

Tables: `FieldMappings`, `ValueLookupTables`, `Templates` (engine = cottle).

---

## Authentication

- **Credential profiles** linked from endpoints; OAuth token cache + refresh job
- Per-institution isolation; audit credential changes (not values)

---

## Reliability, performance, debugging

### Reliability

- **Transactional outbox** (SQL Server): lead + enqueue in one transaction
- At-least-once delivery; config version snapshot on each run
- Dead-letter / exhausted retry state

### Performance

- Async end-to-end; bounded parallelism per institution/endpoint
- Bulk retry jobs with rate limits
- Indexes: `(InstitutionId, Status, CreatedAt)`, `LeadId`, `DeliveryRunId`

### Debugging

- Structured logs: `LeadId`, `DeliveryRunId`, `AttemptId`, `InstitutionId`, `EndpointId`, `ConfigVersion`
- React **delivery timeline**: ingestion → rule match → enrichment → attempts
- OpenTelemetry traces; health checks (DB, Hangfire, OAuth)

---

## React admin UI (minimum)

1. Institutions — CRUD, API keys
2. Field catalog — browse, refresh, approve inferences
3. Enrichment pipelines — steps, SP mapping, test panel
4. Delivery configurations — rules, endpoints, publish workflow
5. Leads & runs — search, retry (failed only / all / selected endpoints)
6. Failures dashboard — by institution, endpoint, error class; CSV export
7. RBAC — institution-scoped roles (viewer, config editor, operator)

**Frontend stack (suggested):** React 18+, TypeScript, React Query, component library (e.g. MUI or Radix + Tailwind), Monaco or CodeMirror for Cottle/XML/JSON.

**Backend:** ASP.NET Core minimal APIs or controllers; OpenAPI for React client generation.

---

## API surface

- `POST /api/v1/leads` — ingest
- `GET /api/v1/leads/{id}`
- `GET /api/v1/delivery-runs` — filters
- `POST /api/v1/delivery-runs/{id}/retry`
- `GET /api/v1/field-catalog` — picker feed (search, tree)
- `POST /api/v1/field-catalog/refresh`
- `POST /api/v1/simulate/match`
- `POST /api/v1/simulate/transform`
- `POST /api/v1/simulate/enrichment`
- `/api/v1/admin/*` — CRUD (secured)

---

## Security & compliance

- PII encryption (TDE and/or column encryption for sensitive payloads)
- TLS outbound; optional IP allowlist per endpoint
- Rate limiting per API key
- Delete/anonymize lead and redact attempt bodies while keeping metrics

---

## Solution structure

```
src/
  LeadDelivery.Api/                 # Ingestion + admin APIs
  LeadDelivery.Worker/              # Outbox, retries, DB pollers, Hangfire
  LeadDelivery.Domain/
  LeadDelivery.Application/         # Routing, enrichment, orchestration
  LeadDelivery.Infrastructure/      # EF Core (SQL Server), Http, OAuth, Cottle, channels
  lead-delivery-admin/              # React SPA
tests/
  LeadDelivery.UnitTests/
  LeadDelivery.IntegrationTests/    # Testcontainers SQL Server + WireMock
```

---

## Example scenario (end-to-end)

A lead from Website A: `Institution=ContosoU`, `Campus=Online`, `Program=MBA`.

1. Ingest → `LeadDocument` updated; new `customAnswers.*` fields inferred → admin approved → visible in React picker.
2. Enrichment runs `ld_GetProgramDetails` → `enriched.ld_GetProgramDetails.*` in catalog.
3. Rule matches **ContosoU Online MBA** config.
4. **Primary** HTTP JSON + OAuth → 201; **Secondary** XML POST → 500 → **PartialSuccess**.
5. Operator retries secondary only from failures dashboard (ContosoU filter).

---

## Acceptance criteria (definition of done)

1. Lead ingested via API and SQL poller; correct config by institution + campus/program.
2. Multi-endpoint config; aggregate status respects primary/secondary rules.
3. HTTP endpoints: JSON, XML, form POST with distinct success code lists.
4. OAuth client credentials with refresh.
5. Field catalog: new column + new JSON key discoverable; picker-only mapping for standard roles.
6. Allowlisted SP with parameter mapping; outputs mappable in React.
7. Enrichment test panel matches runtime; snapshot used on retry.
8. Retry failed endpoints; optional retry with latest config.
9. Failures by institution export; full log correlation for one `leadId`.
10. New **Email** or **Sftp** channel addable via config + executor registration without rewriting orchestration.
11. Load test with documented throughput and p95 metrics.

---

## Deliverables from implementer

1. ADR: rules engine, Cottle, aggregate status, primary failure behavior, SP vs views strategy.
2. ER diagram + **EF Core migrations** (SQL Server).
3. Docker Compose: API, worker, SQL Server, React dev proxy or static hosting.
4. Seed: 2 institutions, 2 configs, WireMock HTTP + sample SP + field catalog seeds.
5. Operator guide for React admin flows.

---

## Open decisions (propose defaults in ADR)

- Hangfire vs Quartz.NET
- JSON column type: `NVARCHAR(MAX)` + `OPENJSON` vs SQL Server JSON type
- Secondaries run when primary fails (default?)
- Max attempt body retention
- Multi-tenant: single DB + `InstitutionId` vs per-tenant DB

---

## One-paragraph elevator pitch (for stakeholders)

The platform ingests leads from APIs and SQL Server, keeps a **living field catalog** so operators map outbound data through a **React field picker**, enriches leads via **pipelines including allowlisted stored procedures**, routes by institution rules to **versioned delivery configs**, and sends through **pluggable channels** (HTTP now, email/SFTP next) with clear **success/partial/fail** status, retries, and institution-scoped failure reporting—all on **.NET**, **EF Core**, and **SQL Server**.