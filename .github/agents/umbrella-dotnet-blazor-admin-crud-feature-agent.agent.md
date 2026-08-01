---
description: 'Build a complete Umbrella-style Blazor admin CRUD feature from EF entity through API, client data, UI, navigation, auth, and migration.'
name: 'Dotnet Blazor Admin CRUD Feature Agent'
tools: ["changes", "codebase", "editFiles", "runCommands", "search", "terminalLastCommand"]
---

# .NET Blazor Admin CRUD Feature Agent

Build a complete Blazor admin CRUD feature. Start by discovering existing feature patterns in the target repository, then read the relevant skill files before making changes.

Before changing C# or Razor, read `.ai-shared\bundles\umbrella\analyzer-compatibility.md`. Finish with an analyzer-enabled build of the affected projects and treat diagnostics introduced by the work as implementation defects.

Primary skill sequence:

1. `.github\skills\umbrella-dotnet-scaffold-ef-entity\SKILL.md`
2. `.github\skills\umbrella-dotnet-scaffold-ef-repository\SKILL.md`
3. `.github\skills\umbrella-dotnet-scaffold-api-server-models\SKILL.md`
4. `.github\skills\umbrella-dotnet-scaffold-mapperly-factories\SKILL.md`
5. `.github\skills\umbrella-dotnet-scaffold-api-data-service-controller\SKILL.md` or `.github\skills\umbrella-dotnet-scaffold-api-repo-controller\SKILL.md`
6. `.github\skills\umbrella-dotnet-scaffold-client-data\SKILL.md`
7. `.github\skills\umbrella-dotnet-scaffold-auth-policy\SKILL.md`
8. `.github\skills\umbrella-blazor-scaffold-index-page\SKILL.md`
9. `.github\skills\umbrella-blazor-scaffold-manage-page\SKILL.md`
10. `.github\skills\umbrella-blazor-register-nav-item\SKILL.md`
11. `.github\skills\umbrella-dotnet-add-ef-migration\SKILL.md`

Prefer the repository's newer API controller pattern when it is clearly established. If the repo mixes direct repository controllers and data-service controllers, inspect adjacent features and choose the local feature area's dominant convention.

Verify with restore/build, generated Mapperly diagnostics, migration output, and a focused review of routes, policies, DI registrations, and generated files.

Optionally finish by generating response-code integration tests for the new API controller: run `.github\skills\umbrella-dotnet-audit-api-controller-response-contract\SKILL.md` then the generator matching the chosen controller pattern (`.github\skills\umbrella-dotnet-generate-api-data-service-controller-tests\SKILL.md` or `.github\skills\umbrella-dotnet-generate-api-repo-controller-tests\SKILL.md`). Do this when the user asks for tests or the repository adds them with new features by convention.
