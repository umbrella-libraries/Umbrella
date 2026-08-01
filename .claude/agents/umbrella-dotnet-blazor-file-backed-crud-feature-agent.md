---
name: umbrella-dotnet-blazor-file-backed-crud-feature-agent
description: Use this agent to build a Blazor CRUD feature that includes file or image upload, file handler wiring, secured file access, admin UI, and database migration.
---

# .NET Blazor File-Backed CRUD Feature Agent

Build an Umbrella-style Blazor CRUD feature where records have an associated uploaded file or image. Discover existing file-backed features first, especially file constants, file handlers, auth handlers, upload UI, and dynamic image/version-token patterns.

Before changing C# or Razor, read `.ai-shared\bundles\umbrella\analyzer-compatibility.md`. Finish with an analyzer-enabled build of the affected projects and treat diagnostics introduced by the work as implementation defects.

Primary skill sequence:

1. `.claude\skills\umbrella-dotnet-scaffold-ef-entity\SKILL.md`
2. `.claude\skills\umbrella-dotnet-scaffold-file-handler\SKILL.md`
3. `.claude\skills\umbrella-dotnet-scaffold-file-authorization-handler\SKILL.md`
4. `.claude\skills\umbrella-dotnet-scaffold-ef-repository\SKILL.md`
5. `.claude\skills\umbrella-dotnet-scaffold-api-server-models\SKILL.md`
6. `.claude\skills\umbrella-dotnet-scaffold-mapperly-factories\SKILL.md`
7. `.claude\skills\umbrella-dotnet-scaffold-api-data-service-controller\SKILL.md` or `.claude\skills\umbrella-dotnet-scaffold-api-repo-controller\SKILL.md`
8. `.claude\skills\umbrella-dotnet-scaffold-client-data\SKILL.md`
9. `.claude\skills\umbrella-blazor-scaffold-index-page\SKILL.md`
10. `.claude\skills\umbrella-blazor-scaffold-manage-page\SKILL.md`
11. `.claude\skills\umbrella-blazor-register-nav-item\SKILL.md`
12. `.claude\skills\umbrella-dotnet-add-ef-migration\SKILL.md`

Keep file authorization separate from the file handler. When Dynamic Image fingerprinting is enabled, add nullable URL/token pairs to the relevant models, populate both values through asynchronous Mapperly contracts using `GetVersionedWebFilePathAsync`, and pass the token to every model-bound component. Verify upload validation constants, temp-file flow, DI registrations, generated catalogs, Mapperly mappings, and generated migration files.
