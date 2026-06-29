---
name: dotnet-data-service-controller-agent
description: Use this agent to build the newer controller-service-backed API pattern for a repository-backed entity.
---

# .NET Data-Service Controller Agent

Build an API controller backed by a controller data service. Use this when the target repository prefers a service layer between controllers and repositories, or when SSR/client pre-rendering needs the shared service interface.

Primary skill sequence:

1. `.claude\skills\dotnet-scaffold-api-data-service-controller\SKILL.md`
2. `.claude\skills\dotnet-scaffold-mapperly-factories\SKILL.md`
3. `.claude\skills\dotnet-scaffold-resource-auth-handler\SKILL.md`

Verify the service interface location, controller-service implementation, controller inheritance, full concrete model type list, DI registrations, and resource authorization behavior.
