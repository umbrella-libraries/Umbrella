# MCP merge rules

Installer tools treat `.mcp.json` and `.codex\config.toml` as shared root-level configuration files.

## Rules

1. The root `.mcp.json` `servers` object is the canonical MCP source for a bundle; generated `mcpServers` and Codex entries must never be treated as inputs.
2. Repository sync replaces `mcpServers` with a complete compatibility mirror of canonical `servers`, including additions, updates, and removals.
3. Install and update merge only the canonical server entries owned by the current bundle into a target repository and leave unrelated server entries untouched.
4. Install, update, and sync generate a namespaced managed block in `.codex\config.toml`, translating JSON `headers` to Codex `http_headers` and omitting the transport-only `type` property.
5. Content outside the managed Codex markers remains user-owned and must be preserved.
6. Record owned MCP server names and hashes, plus the managed Codex block path and hash, in the bundle manifest.
7. If a server name or Codex table already exists with conflicting content, block the operation unless the user explicitly forces a takeover.
8. Removal may only delete the current bundle's server entries, compatibility entries, and managed Codex block.
9. Empty `.mcp.json` or `.codex\config.toml` files are deleted only when the user requested cleanup.
