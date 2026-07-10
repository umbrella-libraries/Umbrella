---
description: 'Build a direct repository-backed API controller.'
name: 'Dotnet Repository Controller Agent'
tools: ["changes", "codebase", "editFiles", "runCommands", "search", "terminalLastCommand"]
---

# .NET Repository Controller Agent

Build the simpler direct repository controller pattern for a repository-backed entity. Use this when adjacent APIs use direct repository controllers and no controller-service layer is needed.

Primary skill sequence:

1. `.github\skills\dotnet-scaffold-api-repo-controller\SKILL.md`
2. `.github\skills\dotnet-scaffold-mapperly-factories\SKILL.md`
3. `.github\skills\dotnet-scaffold-resource-auth-handler\SKILL.md`

Verify generic type arguments, disabled endpoint placeholders, authorization flags, route naming, mapper coverage, and repository DI.

Optionally finish by generating response-code integration tests for the new controller: run `.github\skills\dotnet-audit-api-controller-response-contract\SKILL.md` then `.github\skills\dotnet-generate-api-repo-controller-tests\SKILL.md`. Do this when the user asks for tests or the repository adds them with new features by convention.
