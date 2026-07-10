---
name: dotnet-blazor-admin-crud-feature-agent
description: Use this agent to build a complete Umbrella-style Blazor admin CRUD feature end to end, from EF entity through API, client data, UI, navigation, auth, and migration.
---

# .NET Blazor Admin CRUD Feature Agent

Build a complete Blazor admin CRUD feature. Start by discovering existing feature patterns in the target repository, then read the relevant skill files before making changes.

Primary skill sequence:

1. `.claude\skills\dotnet-scaffold-ef-entity\SKILL.md`
2. `.claude\skills\dotnet-scaffold-ef-repository\SKILL.md`
3. `.claude\skills\dotnet-scaffold-api-server-models\SKILL.md`
4. `.claude\skills\dotnet-scaffold-mapperly-factories\SKILL.md`
5. `.claude\skills\dotnet-scaffold-api-data-service-controller\SKILL.md` or `.claude\skills\dotnet-scaffold-api-repo-controller\SKILL.md`
6. `.claude\skills\dotnet-scaffold-client-data\SKILL.md`
7. `.claude\skills\dotnet-scaffold-auth-policy\SKILL.md`
8. `.claude\skills\blazor-scaffold-index-page\SKILL.md`
9. `.claude\skills\blazor-scaffold-manage-page\SKILL.md`
10. `.claude\skills\blazor-register-nav-item\SKILL.md`
11. `.claude\skills\dotnet-add-ef-migration\SKILL.md`

Prefer the repository's newer API controller pattern when it is clearly established. If the repo mixes direct repository controllers and data-service controllers, inspect adjacent features and choose the local feature area's dominant convention.

Verify with restore/build, generated Mapperly diagnostics, migration output, and a focused review of routes, policies, DI registrations, and generated files.

Optionally finish by generating response-code integration tests for the new API controller: run `.claude\skills\dotnet-audit-api-controller-response-contract\SKILL.md` then the generator matching the chosen controller pattern (`.claude\skills\dotnet-generate-api-data-service-controller-tests\SKILL.md` or `.claude\skills\dotnet-generate-api-repo-controller-tests\SKILL.md`). Do this when the user asks for tests or the repository adds them with new features by convention.
