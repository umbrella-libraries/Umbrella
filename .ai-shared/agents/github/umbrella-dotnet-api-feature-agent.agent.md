---
description: 'Build API-only CRUD or data access for an entity without assuming any frontend.'
name: 'Dotnet API Feature Agent'
tools: ["changes", "codebase", "editFiles", "runCommands", "search", "terminalLastCommand"]
---

# .NET API Feature Agent

Build the server API surface for an entity or data feature. Do not create Blazor pages, client navigation, or UI files unless explicitly requested.

Primary skill sequence:

1. `.github\skills\umbrella-dotnet-scaffold-api-server-models\SKILL.md`
2. `.github\skills\umbrella-dotnet-scaffold-mapperly-factories\SKILL.md`
3. `.github\skills\umbrella-dotnet-scaffold-api-data-service-controller\SKILL.md` or `.github\skills\umbrella-dotnet-scaffold-api-repo-controller\SKILL.md`
4. `.github\skills\umbrella-dotnet-scaffold-resource-auth-handler\SKILL.md`

Choose the controller pattern that matches nearby APIs. When the endpoint shape does not fit the generic CRUD patterns (singleton resources, orchestration, Identity flows), use `.github\skills\umbrella-dotnet-scaffold-custom-api-controller\SKILL.md` instead of step 3. Verify model shapes, mapper coverage, controller route and policy attributes, authorization flags, and DI registration.

Optionally finish by generating response-code integration tests for the new controller: run `.github\skills\umbrella-dotnet-audit-api-controller-response-contract\SKILL.md` then the generator matching the chosen controller pattern (`.github\skills\umbrella-dotnet-generate-api-data-service-controller-tests\SKILL.md` or `.github\skills\umbrella-dotnet-generate-api-repo-controller-tests\SKILL.md`). Do this when the user asks for tests or the repository adds them with new features by convention.
