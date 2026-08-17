# MCP merge rules

Installer tools treat `.mcp.json` and `.codex\config.toml` as shared root-level configuration files.

MCP servers are the one class of managed content that may legitimately be **co-owned**: two bundles
installed into the same repository will routinely both want servers such as `microsoft-learn` or
`playwright`, and neither should have to defer to the other.

## Rules

1. The root `.mcp.json` `servers` object is the canonical MCP source for a bundle; generated `mcpServers` and Codex entries must never be treated as inputs.
2. Repository sync replaces `mcpServers` with a complete compatibility mirror of canonical `servers`, including additions, updates, and removals.
3. Install and update merge only the canonical server entries owned by the current bundle into a target repository and leave unrelated server entries untouched.
4. A server already owned by another bundle is **co-owned** when both bundles describe it identically. Co-ownership needs no force flag; each owner records the server in its own manifest.
5. When two bundles describe the same server name differently, the operation is blocked. The user either aligns the two definitions or forces a takeover.
6. Removal deletes a server entry and its compatibility entry only when no surviving bundle manifest still owns that server. Co-owned servers are retained and reported as retained.
7. Content outside the managed Codex markers remains user-owned and must be preserved.
8. Record owned MCP server names and hashes, plus the Codex region path and this bundle's contribution hash, in the bundle manifest.
9. Empty `.mcp.json` or `.codex\config.toml` files are deleted only when the user requested cleanup.

## The shared Codex MCP region

`.codex\config.toml` cannot use per-bundle managed regions. TOML has no way to declare the same
`[mcp_servers.x]` table twice, so two bundles contributing a shared server would produce an
unparseable document. Codex MCP configuration therefore uses a **single shared region**, not a
namespaced one per bundle:

```toml
# ai-bundle:codex-mcp:start
[mcp_servers."microsoft-learn"]
url = "https://learn.microsoft.com/api/mcp"
# ai-bundle:codex-mcp:end
```

Rules specific to the shared region:

1. The region content is the union of the servers contributed by every installed bundle, rendered from the canonical `.mcp.json` `servers` object.
2. Servers are rendered in ordinal name order, so the result never depends on the order bundles were installed.
3. Rendering translates JSON `headers` to Codex `http_headers` and omits the transport-only `type` property.
4. Install, update, and remove each re-render the whole region from the manifests of all installed bundles. A bundle rewriting the region is not taking ownership of another bundle's servers; it is recomputing a derived value.
5. A manifest records the hash of **its own contribution** rendered in isolation, never the hash of the whole region. Another bundle installing or removing therefore never registers as drift against this bundle.
6. A server declared inside the region that no installed manifest accounts for is user-authored content and blocks the operation unless forced.
7. `[mcp_servers.*]` tables outside the region are user-owned and block the operation unless forced.

### Legacy per-bundle regions

Earlier tool versions wrote a namespaced `# ai-bundle:<bundle-id>:codex-mcp:start` region per bundle.
Install and update absorb any such region, regardless of bundle id, and replace it with the shared
region. This is safe because every server a legacy region declared is also present in `.mcp.json`, so
the union re-render reproduces it. The migration is one-way and needs no user action.
