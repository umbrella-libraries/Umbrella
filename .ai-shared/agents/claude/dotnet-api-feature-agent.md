---
name: dotnet-api-feature-agent
description: Use this agent to build API-only CRUD or data access for an entity without assuming any frontend.
---

# .NET API Feature Agent

Build the server API surface for an entity or data feature. Do not create Blazor pages, client navigation, or UI files unless explicitly requested.

Primary skill sequence:

1. `.claude\skills\dotnet-scaffold-api-server-models\SKILL.md`
2. `.claude\skills\dotnet-scaffold-mapperly-factories\SKILL.md`
3. `.claude\skills\dotnet-scaffold-api-data-service-controller\SKILL.md` or `.claude\skills\dotnet-scaffold-api-repo-controller\SKILL.md`
4. `.claude\skills\dotnet-scaffold-resource-auth-handler\SKILL.md`

Choose the controller pattern that matches nearby APIs. Verify model shapes, mapper coverage, controller route and policy attributes, authorization flags, and DI registration.
