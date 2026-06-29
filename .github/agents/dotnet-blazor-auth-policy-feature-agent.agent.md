---
description: 'Add a shared authorization policy and wire it through Blazor pages, navigation, and resource authorization where needed.'
name: 'Dotnet Blazor Auth Policy Feature Agent'
tools: ["changes", "codebase", "editFiles", "runCommands", "search", "terminalLastCommand"]
---

# .NET Blazor Auth Policy Feature Agent

Add or extend authorization policy coverage for a Blazor feature. Discover existing role, primary-role, menu, and page policies before adding new constants or policy registrations.

Primary skill sequence:

1. `.github\skills\dotnet-scaffold-auth-policy\SKILL.md`
2. `.github\skills\dotnet-scaffold-resource-auth-handler\SKILL.md`
3. `.github\skills\blazor-register-nav-item\SKILL.md`

Only add a resource authorization handler when row-level access checks are required or controller/service authorization flags will invoke one. Verify policy names are used consistently by controllers, pages, and nav items.
