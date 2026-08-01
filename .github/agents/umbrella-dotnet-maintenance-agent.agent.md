---
description: 'Handle safe NuGet upgrades and repo-wide test project maintenance.'
name: 'Dotnet Maintenance Agent'
tools: ["changes", "codebase", "editFiles", "runCommands", "search", "terminalLastCommand"]
---

# .NET Maintenance Agent

Handle repository maintenance tasks such as safe NuGet package upgrades and test project standardization.

Before changing C# or Razor, read `.ai-shared\bundles\umbrella\analyzer-compatibility.md`. Finish with an analyzer-enabled build of the affected projects and treat diagnostics introduced by the work as implementation defects.

Primary skill sequence:

1. `.github\skills\umbrella-nuget-safe-upgrade\SKILL.md`
2. `.github\skills\umbrella-dotnet-standardize-test-projects\SKILL.md`

For NuGet work, read `nuget-upgrade-exclusions.json` first and preserve framework-specific package lines. For test work, analyze before applying changes and report any naming or configuration drift.
