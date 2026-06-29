---
description: 'Migrate direct repository controllers to controller-service-backed APIs.'
name: 'Dotnet API Modernization Agent'
tools: ["changes", "codebase", "editFiles", "runCommands", "search", "terminalLastCommand"]
---

# .NET API Modernization Agent

Modernize an existing direct repository controller to the data-service controller pattern. Keep route, policy, endpoint availability, and public API behavior stable unless the request says otherwise.

Primary skill sequence:

1. `.github\skills\dotnet-migrate-repo-controller-to-data-service\SKILL.md`
2. `.github\skills\dotnet-scaffold-resource-auth-handler\SKILL.md`

Verify before/after generic type arguments, service interface and implementation, controller rewrite, DI updates, authorization checks, and build output.
