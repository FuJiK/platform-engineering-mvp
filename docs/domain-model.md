# Domain Model Design

`domain/service.edn` is the only input to the Platform Engineering MVP compiler.

## Schema

| Key | Type | Required | Description |
|-----|------|----------|-------------|
| `:service` | keyword | yes | Logical service name; becomes Docker container name |
| `:environment` | keyword | yes | Deployment environment label (e.g. `:local`) |
| `:container` | map | yes | Container image and port mapping |
| `:operations` | map | yes | Day-2 operations and role bindings |

### `:container`

| Key | Type | Constraints |
|-----|------|-------------|
| `:image` | string | Non-empty Docker image reference |
| `:host-port` | int | 1–65535 |
| `:container-port` | int | 1–65535 |

### `:operations`

Supported operations (MVP):

| Operation | Purpose | Extra keys |
|-----------|---------|------------|
| `:get-status` | Read container state | `:roles` |
| `:get-logs` | Read recent logs | `:roles`, `:max-lines` (1–1000) |
| `:restart-service` | Restart container | `:roles`, `:approval` must be `:required` |

Any other operation key (e.g. `:delete-service`) is rejected by `policy/validate-domain!`.

### Roles

- `:operator` — read-only operations
- `:sre` — includes `:restart-service` with explicit approval

Roles are declared per operation as a set of keywords.

## Generated Outputs

```bash
clojure -M:generate
```

| Output | Consumer |
|--------|----------|
| `generated/main.tf.json` | Terraform (Docker provider) |
| `generated/ops-policy.json` | MCP server (`PLATFORM_ROLE` runtime) |

## Extension Guidelines

When adding a new operation:

1. Add to `policy/supported-operations` only if safe for the MVP threat model.
2. Implement MCP tool handler and Docker adapter function.
3. Extend `compiler/build-ops-policy` and tests.
4. Document approval / rate-limit requirements in ADR or security checklist.

Do **not** add destructive operations to the DSL. Prefer external approval workflows
(Phase 5) instead of exposing delete/destroy tools.

## Example

See `domain/service.edn` and `examples/` for valid patterns.
