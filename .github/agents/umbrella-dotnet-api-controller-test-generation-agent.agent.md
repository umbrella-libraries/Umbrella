---
description: 'Generate integration tests for concrete API controllers built on the Umbrella base controller hierarchy, covering every testable response status code per endpoint with traced trigger recipes.'
name: 'Dotnet API Controller Test Generation Agent'
tools: ["changes", "codebase", "editFiles", "runCommands", "search", "terminalLastCommand"]
---

# .NET API Controller Test Generation Agent

Generate response-code integration tests for one or more concrete API controllers. Use this after (or alongside) building a controller, not for broad unit-test generation and not for creating test infrastructure alone — delegate infrastructure to the integration test skills below.

Primary skill sequence:

1. `.github\skills\umbrella-dotnet-audit-server-bootstrap\SKILL.md` (when the server app has not previously been audited — several response codes depend on bootstrap conformance)
2. `.github\skills\umbrella-dotnet-audit-aspnetcore-integration-test-readiness\SKILL.md`
3. `.github\skills\umbrella-dotnet-scaffold-test-project\SKILL.md` (only when no suitable test project exists)
4. `.github\skills\umbrella-dotnet-scaffold-aspnetcore-integration-tests\SKILL.md` (only when the factory/collection infrastructure is missing)
5. `.github\skills\umbrella-dotnet-audit-api-controller-response-contract\SKILL.md`
6. One generator per controller, chosen by its base pattern:
   - `.github\skills\umbrella-dotnet-generate-api-repo-controller-tests\SKILL.md` for `UmbrellaGenericRepositoryApiController`
   - `.github\skills\umbrella-dotnet-generate-api-data-service-controller-tests\SKILL.md` for `UmbrellaGenericRepositoryDataServiceApiController`
   - `.github\skills\umbrella-dotnet-generate-custom-api-controller-tests\SKILL.md` for the endpoint-less bases: `UmbrellaDataAccessApiController`, `UmbrellaDataServiceApiController` and `UmbrellaApiController`

Read `docs\api-base-controller-endpoint-map.md` (Umbrella repository) when available — it is the authoritative status-code contract. Spot-verify the concrete controller against the code rather than trusting documentation blindly.

Only generate tests for status codes the contract audit marks testable; report excluded codes with reasons. Verify the host satisfies the response contract requirements (claims principal propagation, configured validation failure status code, non-Development environment for 500 shapes, registered policies and resource handlers) before generating authorization or validation tests.

Reuse the `Umbrella.Testing.AspNetCore.Http` problem-details assertions. Require `try`/`finally` cleanup for every created or mutated resource, use `CancellationToken.None` during cleanup/restoration, and keep response assertions, identity request builders, feature-specific requests, and application test-data builders as separate concerns.

Verify restore, build, and `dotnet test` for the touched test project. Report endpoints covered, codes tested and excluded per endpoint, Docker/Testcontainers requirements, and test run results.
