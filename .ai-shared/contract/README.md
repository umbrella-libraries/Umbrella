# AI bundle contract

This directory documents the shared on-disk contract used by installer tools that add AI agents, skills, and related assets to a target repository.

## Core rules

1. Each installed bundle must have a stable namespaced `bundleId`.
2. Each tool may only update or remove files, managed doc blocks, and MCP server entries that are recorded in its own manifest. The shared Codex MCP region is the sole exception: it is a derived value recomputed from every installed manifest, so any tool may rewrite it in full.
3. Shared root docs are composed from bundle-specific managed blocks rather than replaced wholesale.
4. `.mcp.json` is merged by server entry ownership rather than overwritten wholesale. Server entries may be co-owned by several bundles when all owners describe them identically, and are deleted only when the last owner is removed.
5. Collision detection must block changes that would overwrite content owned by another bundle unless the user explicitly forces a takeover. Identical co-ownable MCP server definitions are not a collision.

## Source-of-truth files

- Bundle block fragments live under `.ai-shared\bundles\<bundle-id>\blocks\`.
- A bundle definition may identify a canonical root `.mcp.json`; its `servers` object is the only editable MCP source.
- Compatibility `mcpServers` entries and namespaced `.codex\config.toml` MCP blocks are generated from that canonical `servers` object.
- Bundle manifests are written to target repos under `.ai-shared\bundles\<bundle-id>\manifest.json`.
- The manifest schema for installed bundles is defined in `bundle-manifest.schema.json`.

## Managed markers

Managed blocks in shared docs use namespaced markers:

- `<!-- ai-bundle:<bundle-id>:start -->`
- `<!-- ai-bundle:<bundle-id>:end -->`

Anything outside those markers remains user-owned.

The Codex MCP region uses TOML comments as markers. It is deliberately **not** namespaced per bundle,
because TOML cannot declare the same `[mcp_servers.x]` table twice:

- `# ai-bundle:codex-mcp:start`
- `# ai-bundle:codex-mcp:end`

Its content is the union of the servers contributed by every installed bundle. Unrelated TOML outside
those markers remains user-owned. Legacy `# ai-bundle:<bundle-id>:codex-mcp:*` regions written by
earlier tool versions are absorbed into the shared region on install or update.
