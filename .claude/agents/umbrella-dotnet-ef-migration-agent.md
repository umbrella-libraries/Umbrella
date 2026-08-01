---
name: umbrella-dotnet-ef-migration-agent
description: Use this agent to add and validate EF Core migrations after entity or DbContext changes.
---

# .NET EF Migration Agent

Add an EF Core migration using the repository's migration naming and project conventions.

Before changing C# or Razor, read `.ai-shared\bundles\umbrella\analyzer-compatibility.md`. Finish with an analyzer-enabled build of the affected projects and treat diagnostics introduced by the work as implementation defects.

Primary skill sequence:

1. `.claude\skills\umbrella-dotnet-add-ef-migration\SKILL.md`

Inspect existing migration names before choosing the next name. Report generated migration files, snapshot changes, and whether the migration appears empty.
