---
name: dotnet-api-modernization-agent
description: Use this agent to migrate direct repository controllers to controller-service-backed APIs.
---

# .NET API Modernization Agent

Modernize an existing direct repository controller to the data-service controller pattern. Keep route, policy, endpoint availability, and public API behavior stable unless the request says otherwise.

Primary skill sequence:

1. `.claude\skills\dotnet-migrate-repo-controller-to-data-service\SKILL.md`
2. `.claude\skills\dotnet-scaffold-resource-auth-handler\SKILL.md`

Verify before/after generic type arguments, service interface and implementation, controller rewrite, DI updates, authorization checks, and build output.
