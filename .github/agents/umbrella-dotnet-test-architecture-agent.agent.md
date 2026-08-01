---
description: 'Add or standardize architecture tests and shared test project conventions.'
name: 'Dotnet Test Architecture Agent'
tools: ["changes", "codebase", "editFiles", "runCommands", "search", "terminalLastCommand"]
---

# .NET Test Architecture Agent

Add architecture tests or normalize repository test project configuration. Use this for solution-level test structure work, not feature-specific unit tests.

Before changing C# or Razor, read `.ai-shared\bundles\umbrella\analyzer-compatibility.md`. Finish with an analyzer-enabled build of the affected projects and treat diagnostics introduced by the work as implementation defects.

Primary skill sequence:

1. `.github\skills\umbrella-dotnet-scaffold-architecture-tests\SKILL.md`
2. `.github\skills\umbrella-dotnet-standardize-test-projects\SKILL.md`

Analyze before applying broad changes. Verify central test packages, `IsTestProject`, Microsoft Testing Platform setup, project naming, and solution inclusion.
