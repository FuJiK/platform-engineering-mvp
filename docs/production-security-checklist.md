# Production Security Checklist

Use this checklist when moving from the local Docker MVP to a production platform.

## Domain & Policy

- [ ] Domain model reviewed; no destructive operations in DSL
- [ ] Generated ops policy version-controlled and signed (or stored in secure config service)
- [ ] Role definitions mapped to real identity provider groups (OIDC / Entra / IAM)

## Authentication & Authorization

- [ ] MCP clients authenticate via OIDC or mTLS
- [ ] `PLATFORM_ROLE` replaced with token claims (not environment variables)
- [ ] Cloud IAM / Managed Identity generated from domain model (Phase 3)
- [ ] Least-privilege policies per role; no shared admin credentials

## Operations Safety

- [ ] `restart_service` requires approval workflow (Phase 5 Approval Service)
- [ ] Rate limits on mutation tools
- [ ] Idempotency keys for mutating operations
- [ ] No shell string concatenation; ProcessBuilder-style argument arrays only

## Audit & Observability

- [ ] All MCP tool calls logged with actor, tool, arguments hash, outcome
- [ ] Terraform apply/destroy audited separately from MCP operations
- [ ] Alerts on policy violations and repeated authorization failures

## Infrastructure

- [ ] Terraform remote state with locking
- [ ] Secrets in vault (not env files or repo)
- [ ] Network isolation for managed workloads
- [ ] Container image pinning and vulnerability scanning

## MCP Gateway (Phase 4)

- [ ] Central MCP Gateway / Catalog in front of service MCP servers
- [ ] Protocol version negotiation documented and tested
- [ ] Tool discovery restricted to catalog-approved tools

## Pre-Go-Live Review

- [ ] Penetration test on MCP surface
- [ ] Disaster recovery runbook (terraform destroy + reprovision)
- [ ] On-call runbook for failed applies and stuck containers
