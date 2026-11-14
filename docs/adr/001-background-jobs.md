# ADR 001: Background jobs — Hangfire

## Status

Accepted

## Context

Delivery processing, retries, and outbox polling need a durable job scheduler.

## Decision

Use **Hangfire** with SQL Server storage (same database as application data).

## Consequences

- Worker and API can share Hangfire dashboard (API exposes it in Development).
- Quartz remains an alternative if Hangfire limits are hit later.