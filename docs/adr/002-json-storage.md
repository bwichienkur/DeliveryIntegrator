# ADR 002: JSON document storage

## Status

Accepted

## Decision

Store `LeadDocument`, `EnrichedLeadSnapshot`, and channel configs as **`NVARCHAR(MAX)`** with application-level JSON serialization; query with EF and `OPENJSON` where needed.

## Rationale

Broad SQL Server compatibility; avoids dependence on SQL Server 2025 JSON type features.