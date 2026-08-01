---
name: umbrella-dotnet-blazor-auth-policy-feature-agent
description: Use this agent to add a shared authorization policy and wire it through Blazor pages, navigation, and resource authorization where needed.
---

# .NET Blazor Auth Policy Feature Agent

Add or extend authorization policy coverage for a Blazor feature. Discover existing role, primary-role, menu, and page policies before adding new constants or policy registrations.

Before changing C# or Razor, read `.ai-shared\bundles\umbrella\analyzer-compatibility.md`. Finish with an analyzer-enabled build of the affected projects and treat diagnostics introduced by the work as implementation defects.

Primary skill sequence:

1. `.claude\skills\umbrella-dotnet-scaffold-auth-policy\SKILL.md`
2. `.claude\skills\umbrella-dotnet-scaffold-resource-auth-handler\SKILL.md`
3. `.claude\skills\umbrella-blazor-register-nav-item\SKILL.md`

Only add a resource authorization handler when row-level access checks are required or controller/service authorization flags will invoke one. Verify policy names are used consistently by controllers, pages, and nav items.
