---
name: dotnet-core-service-agent
description: Use this agent to build a non-CRUD core/domain service with logic-layer models and repository access where needed.
---

# .NET Core Service Agent

Build a domain or application service in the Core.Logic layer. Keep web/API/shared UI models out of core service interfaces and implementations.

Primary skill sequence:

1. `.claude\skills\dotnet-scaffold-service\SKILL.md`
2. `.claude\skills\dotnet-scaffold-ef-repository\SKILL.md`

Only scaffold a repository when the service needs data access that is not already available. Verify layer boundaries, DI registration, cancellation tokens, logging/error patterns, and tests or focused build coverage.
