# Domain Model Examples

These EDN files are valid alternatives to `domain/service.edn`. Each passes
`policy/validate-domain!`.

## Files

| File | Use case |
|------|----------|
| `minimal-web.edn` | Default-like nginx on `:8080` |
| `read-only-operator.edn` | No restart; operator read-only only |
| `alternate-port.edn` | nginx on `:8888` |

## Generate from an example

Copy or pass the path to the compiler main:

```bash
cp examples/minimal-web.edn domain/service.edn
clojure -M:generate
```

Or generate directly:

```bash
clojure -M -m platform-mvp.compiler examples/read-only-operator.edn
```

Outputs still land in `generated/` (same as the default flow).

## Validate all examples

```bash
clojure -M:test
```

The `examples-test` namespace loads every `examples/*.edn` through
`policy/validate-domain!`.
