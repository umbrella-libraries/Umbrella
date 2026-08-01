---
description: 'Migrate legacy Blazor client data types from Repository naming and folders to Service naming and folders.'
name: 'Dotnet Blazor Client Data Modernization Agent'
tools: ["changes", "codebase", "editFiles", "runCommands", "search", "terminalLastCommand"]
---

# .NET Blazor Client Data Modernization Agent

Modernize a specific Blazor client data feature from the legacy `Repositories` convention to the `Services` convention without changing behavior.

Before changing C# or Razor, read `.ai-shared\bundles\umbrella\analyzer-compatibility.md`. Finish with an analyzer-enabled build of the affected projects and treat diagnostics introduced by the work as implementation defects.

Primary skill sequence:

1. `.github\skills\umbrella-dotnet-rename-client-repository-to-service\SKILL.md`

Before editing, inventory all interface, implementation, DI, namespace, and Blazor component references. Verify that generated client behavior remains the same and that no stale repository references remain.
