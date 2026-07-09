---
description: 'Build the newer controller-service-backed API pattern for a repository-backed entity.'
name: 'Dotnet Data Service Controller Agent'
tools: ["changes", "codebase", "editFiles", "runCommands", "search", "terminalLastCommand"]
---

# .NET Data-Service Controller Agent

Build an API controller backed by a controller data service. Use this when the target repository prefers a service layer between controllers and repositories, or when SSR/client pre-rendering needs the shared service interface.

Primary skill sequence:

1. `.github\skills\dotnet-scaffold-api-data-service-controller\SKILL.md`
2. `.github\skills\dotnet-scaffold-mapperly-factories\SKILL.md`
3. `.github\skills\dotnet-scaffold-resource-auth-handler\SKILL.md`

Verify the service interface location, controller-service implementation, controller inheritance, full concrete model type list, DI registrations, and resource authorization behavior.

Optionally finish by generating response-code integration tests for the new controller: run `.github\skills\dotnet-audit-api-controller-response-contract\SKILL.md` then `.github\skills\dotnet-generate-data-service-controller-tests\SKILL.md`. Do this when the user asks for tests or the repository adds them with new features by convention.
