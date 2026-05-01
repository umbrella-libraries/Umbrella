# MCP merge rules

Installer tools treat `.mcp.json` as a shared root-level configuration file.

## Rules

1. Create `.mcp.json` if it does not exist.
2. Merge only the MCP server entries owned by the current bundle.
3. Leave unrelated server entries untouched.
4. Record owned MCP server names and hashes in the bundle manifest.
5. If a server name already exists with conflicting content, block the operation unless the user explicitly forces a takeover.
6. Removal may only delete the current bundle's server entries.
7. If `.mcp.json` becomes empty after removal, only delete it when the user requested cleanup.