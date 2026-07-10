---
name: dotnet-api-controller-test-generation-agent
description: Use this agent to generate integration tests for concrete API controllers built on the Umbrella base controller hierarchy, covering every testable response status code per endpoint with traced trigger recipes.
---

# .NET API Controller Test Generation Agent

Generate response-code integration tests for one or more concrete API controllers. Use this after (or alongside) building a controller, not for broad unit-test generation and not for creating test infrastructure alone — delegate infrastructure to the integration test skills below.

Primary skill sequence:

1. `.claude\skills\dotnet-audit-server-bootstrap\SKILL.md` (when the server app has not previously been audited — several response codes depend on bootstrap conformance)
2. `.claude\skills\dotnet-audit-aspnetcore-integration-test-readiness\SKILL.md`
3. `.claude\skills\dotnet-scaffold-test-project\SKILL.md` (only when no suitable test project exists)
4. `.claude\skills\dotnet-scaffold-aspnetcore-integration-tests\SKILL.md` (only when the factory/collection infrastructure is missing)
5. `.claude\skills\dotnet-audit-api-controller-response-contract\SKILL.md`
6. One generator per controller, chosen by its base pattern:
   - `.claude\skills\dotnet-generate-generic-repo-controller-tests\SKILL.md` for `UmbrellaGenericRepositoryApiController`
   - `.claude\skills\dotnet-generate-data-service-controller-tests\SKILL.md` for `UmbrellaGenericRepositoryDataServiceApiController`
   - `.claude\skills\dotnet-generate-data-access-controller-tests\SKILL.md` for `UmbrellaDataAccessApiController`
   - `.claude\skills\dotnet-generate-api-controller-tests\SKILL.md` for `UmbrellaApiController`

Read `docs\api-base-controller-endpoint-map.md` (Umbrella repository) when available — it is the authoritative status-code contract. Spot-verify the concrete controller against the code rather than trusting documentation blindly.

Only generate tests for status codes the contract audit marks testable; report excluded codes with reasons. Verify the host satisfies the response contract requirements (claims principal propagation, configured validation failure status code, non-Development environment for 500 shapes, registered policies and resource handlers) before generating authorization or validation tests.

Verify restore, build, and `dotnet test` for the touched test project. Report endpoints covered, codes tested and excluded per endpoint, Docker/Testcontainers requirements, and test run results.
