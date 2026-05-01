# AI bundle contract

This directory documents the shared on-disk contract used by installer tools that add AI agents, skills, and related assets to a target repository.

## Core rules

1. Each installed bundle must have a stable namespaced `bundleId`.
2. Each tool may only update or remove files, managed doc blocks, and MCP server entries that are recorded in its own manifest.
3. Shared root docs are composed from bundle-specific managed blocks rather than replaced wholesale.
4. `.mcp.json` is merged by server entry ownership rather than overwritten wholesale.
5. Collision detection must block changes that would overwrite content owned by another bundle unless the user explicitly forces a takeover.

## Source-of-truth files

- Bundle block fragments live under `.ai-shared\bundles\<bundle-id>\blocks\`.
- Bundle MCP templates live under `.ai-shared\bundles\<bundle-id>\mcp\`.
- Bundle manifests are written to target repos under `.ai-shared\bundles\<bundle-id>\manifest.json`.
- The manifest schema for installed bundles is defined in `bundle-manifest.schema.json`.

## Managed markers

Managed blocks in shared docs use namespaced markers:

- `<!-- ai-bundle:<bundle-id>:start -->`
- `<!-- ai-bundle:<bundle-id>:end -->`

Anything outside those markers remains user-owned.