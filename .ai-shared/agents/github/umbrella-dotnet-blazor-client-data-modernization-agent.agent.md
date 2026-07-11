---
description: 'Migrate legacy Blazor client data types from Repository naming and folders to Service naming and folders.'
name: 'Dotnet Blazor Client Data Modernization Agent'
tools: ["changes", "codebase", "editFiles", "runCommands", "search", "terminalLastCommand"]
---

# .NET Blazor Client Data Modernization Agent

Modernize a specific Blazor client data feature from the legacy `Repositories` convention to the `Services` convention without changing behavior.

Primary skill sequence:

1. `.github\skills\umbrella-dotnet-rename-client-repository-to-service\SKILL.md`

Before editing, inventory all interface, implementation, DI, namespace, and Blazor component references. Verify that generated client behavior remains the same and that no stale repository references remain.
