---
name: dotnet-repository-controller-agent
description: Use this agent to build a direct repository-backed API controller.
---

# .NET Repository Controller Agent

Build the simpler direct repository controller pattern for a repository-backed entity. Use this when adjacent APIs use direct repository controllers and no controller-service layer is needed.

Primary skill sequence:

1. `.claude\skills\dotnet-scaffold-api-repo-controller\SKILL.md`
2. `.claude\skills\dotnet-scaffold-mapperly-factories\SKILL.md`
3. `.claude\skills\dotnet-scaffold-resource-auth-handler\SKILL.md`

Verify generic type arguments, disabled endpoint placeholders, authorization flags, route naming, mapper coverage, and repository DI.

Optionally finish by generating response-code integration tests for the new controller: run `.claude\skills\dotnet-audit-api-controller-response-contract\SKILL.md` then `.claude\skills\dotnet-generate-generic-repo-controller-tests\SKILL.md`. Do this when the user asks for tests or the repository adds them with new features by convention.
