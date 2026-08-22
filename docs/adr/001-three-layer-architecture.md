# ADR 001: Three-Layer Architecture (Domain → IaC → MCP)

## Status

Accepted

## Context

Platform Engineering MVP must prove that a single declarative input can drive both
provisioning (Terraform) and day-2 operations (MCP tools), while keeping dangerous
operations out of the system by design—not only by permission checks.

## Decision

Adopt a three-layer architecture:

```text
domain/service.edn
        │
        ▼
Clojure Platform Core (compiler + policy)
        │
   ┌────┴────┐
   ▼         ▼
Terraform   ops-policy.json
   │         │
   ▼         ▼
Docker      MCP Server
```

1. **Domain Model** (`domain/service.edn`) is the single source of truth.
2. **Compiler** generates `generated/main.tf.json` and `generated/ops-policy.json`.
3. **MCP Server** reads ops policy at runtime and exposes only approved tools.

Dangerous operations (delete, destroy, arbitrary shell) are excluded at the DSL level,
not implemented as MCP tools, and re-validated at execution time.

## Consequences

### Positive

- One edit point for provisioning and operations policy.
- Security constraints are enforced in multiple layers (DSL, codegen, MCP runtime).
- Local Docker MVP can later swap adapters (ECS, App Service) without changing the DSL shape.

### Negative

- Every new operation requires compiler, policy, and MCP changes.
- Generated artifacts must be regenerated after domain edits (`clojure -M:generate`).

## Alternatives Considered

| Alternative | Why rejected |
|-------------|--------------|
| Hand-written Terraform + separate ops config | Drift between provisioning and operations |
| MCP-only with runtime IAM | Does not prove domain-driven platform engineering |
| Shell-based MCP tools | Violates ProcessBuilder / no-shell-injection requirement |
