# Managed block format

Managed blocks are the safe unit of sharing for root instruction files such as `AGENTS.md`, `CLAUDE.md`, and `.github\copilot-instructions.md`.

## Marker format

```md
<!-- ai-bundle:<bundle-id>:start -->
... bundle-managed content ...
<!-- ai-bundle:<bundle-id>:end -->
```

## Rules

1. Marker values must include the stable bundle id.
2. Install and update operations may only replace the content between matching markers for the current bundle.
3. Removal may only delete the current bundle's marked block.
4. Content outside the markers remains user-owned.
5. If a marked block has been manually edited since installation, the installer should block the update unless the user explicitly forces it.
6. Several bundles may contribute blocks to the same document. Each block is independent and identified by its own bundle id, so the order of installation does not matter.

## Exception: the shared Codex MCP region

Doc blocks are namespaced per bundle because Markdown tolerates any number of them. `.codex\config.toml`
is the one managed region that is **not** namespaced, because TOML cannot express the same
`[mcp_servers.x]` table twice. It uses a single shared, co-owned region instead. See
`mcp-merge-rules.md` for its ownership and migration rules.