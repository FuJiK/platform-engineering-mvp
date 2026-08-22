# Production Security Roadmap

This document expands the README phase plan with concrete deliverables and
security controls for production hardening.

## Phase Overview

| Phase | Focus | Key security outcomes |
|-------|--------|------------------------|
| 1 | Docker + nginx (local MVP) | Policy-as-code, no destructive MCP tools |
| 2 | Cloud runtime (ECS / App Service) | Managed runtime, no local Docker socket |
| 3 | IAM / Managed Identity from domain | Least privilege generated from EDN |
| 4 | MCP Gateway + Catalog | Central tool discovery and policy enforcement |
| 5 | Approval / Audit / Rate Limit | Human-in-the-loop mutations, full audit trail |
| 6 | AI Operator | Governed autonomous operations |

---

## Phase 2: Cloud Runtime Adapter

**Goal:** Replace local Docker with a managed container service.

### Deliverables

- Terraform modules for AWS ECS or Azure App Service
- Adapter interface in compiler (swap `build-terraform-json` backend)
- Health check integration for MCP `get_status`

### Security

- No Docker socket exposure to MCP process
- Workload identity for container pulls
- Network security groups / private endpoints

---

## Phase 3: IAM / Managed Identity Generation

**Goal:** Generate cloud IAM policies from `:operations` role bindings.

### Deliverables

- Map `:operator` / `:sre` to IdP groups (OIDC claims)
- Generate IAM policy documents per role and operation
- Terraform outputs for role ARNs linked to ops policy

### Security controls

| Control | Implementation |
|---------|----------------|
| Least privilege | One policy per operation class (read vs mutate) |
| No long-lived keys | Managed Identity / IRSA / workload identity only |
| Separation of duties | Operator cannot assume SRE mutation policies |

### Example mapping

```text
:operations :get-status {:roles #{:operator :sre}}
  → iam:DescribeTasks, logs:GetLogEvents (read)

:operations :restart-service {:roles #{:sre}}
  → ecs:UpdateService (mutate) + approval gate (Phase 5)
```

---

## Phase 4: MCP Gateway + Catalog

**Goal:** Single entry point for all MCP clients; catalog defines allowed tools.

### Deliverables

- MCP Gateway proxy (auth termination, protocol version header)
- Tool catalog synced from generated `ops-policy.json`
- Per-tenant / per-environment catalog partitions

### Security controls

- Authenticate all clients (OIDC / mTLS)
- Strip or reject tools not in catalog
- Protocol version enforcement (`MCP-Protocol-Version` header for HTTP transport)

---

## Phase 5: Approval / Audit / Rate Limit

**Goal:** Safe mutations with accountability.

### Approval Service

| Operation | Requirement |
|-----------|-------------|
| `restart_service` | Ticket ID or approval token in MCP arguments |
| Future mutators | Two-person rule for production |

Flow:

```text
MCP tools/call (restart_service)
  → Approval Service validates token
  → Audit log write (pending)
  → Execute mutation
  → Audit log write (completed / failed)
```

### Audit

- Structured logs: `actor`, `role`, `tool`, `arguments_hash`, `result`, `trace_id`
- Immutable store (S3 Object Lock, Azure immutable blob, or SIEM)
- Alert on policy denial spikes

### Rate limiting

| Scope | Limit example |
|-------|----------------|
| Per actor | 10 `get_logs` / minute |
| Per service | 3 `restart_service` / hour |
| Global | Circuit breaker on error rate |

---

## Phase 6: AI Operator

**Goal:** AI agents operate infrastructure through the same policy stack—not bypassing it.

### Deliverables

- Agent identity (Entra Agent ID / workload principal)
- Prompt + tool allowlists from catalog
- Automated runbooks as MCP tool sequences (read-only by default)

### Security controls

- Agents never receive SRE mutation role by default
- Human approval queue for agent-requested restarts
- Full prompt + tool call retention for forensics

---

## Migration Checkpoints (MVP → Production)

1. [ ] Replace `PLATFORM_ROLE` env var with OIDC claims
2. [ ] Enable remote Terraform state + locking
3. [ ] Deploy MCP behind Gateway with auth
4. [ ] Wire Approval Service to `restart_service`
5. [ ] Complete [production-security-checklist.md](production-security-checklist.md)
6. [ ] Run game-day: failed apply, policy denial, approval timeout

See also: [production-security-checklist.md](production-security-checklist.md)
