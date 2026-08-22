# MCP Protocol Version

The MCP server advertises protocol version **`2025-11-25`**.

## Why this version?

This is **not a placeholder**. It is the [MCP specification release
2025-11-25](https://modelcontextprotocol.io/specification/2025-11-25), used for
`initialize` request/response negotiation per the
[lifecycle](https://modelcontextprotocol.io/specification/2025-11-25/basic/lifecycle)
section.

If a client sends a different `protocolVersion`, the server responds with the
version it supports (see `initialize-result` in `platform_mvp/mcp.clj`).

## Manual testing

Use the same version in manual JSON-RPC requests (see README §5). Example:

```json
{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"manual-test","version":"0.1.0"}}}
```

## Upgrading

When adopting a newer MCP spec (e.g. 2026-xx-xx), update:

1. `protocol-version` in `src/platform_mvp/mcp.clj`
2. Demo scripts and README examples
3. Client configs (`mcp-client-config.example.json`, VS Code MCP settings)

Run `clojure -M:test` after changes—`mcp-test` asserts the negotiated version.
