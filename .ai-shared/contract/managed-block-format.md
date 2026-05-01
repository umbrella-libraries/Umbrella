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