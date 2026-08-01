---
name: umbrella-dotnet-standardize-test-projects
description: 'Analyze, set up, or update .NET test project configuration across a solution to follow the shared Umbrella IsTestProject=true pattern, singular .Test naming, and xUnit v3 Microsoft Testing Platform conventions.'
---

# Standardize Test Projects

## Purpose

Normalize solution-wide .NET test configuration so runnable test projects opt in with `<IsTestProject>true</IsTestProject>` and shared repo-level configuration provides the common runner setup, packages, output type, packing behavior, warning policy, and Microsoft Testing Platform `dotnet test` opt-in.

## Assets

- `scripts\Invoke-StandardizeTestProjects.ps1`

## Workflow

1. Start with `Analyze` mode unless the user explicitly requested edits.
2. Inspect `global.json`, `Directory.Build.props`, `Directory.Build.targets`, and `Directory.Packages.props`.
3. Inventory `.csproj` files under `Test` / `test` folders or with `.Test` / `.Tests` names.
4. Classify projects as runnable tests, helpers, or ambiguous:
   - Runnable tests use `<IsTestProject>true</IsTestProject>`.
   - Helper projects use `<IsTestProject>false</IsTestProject>` when they live under test folders but are not runnable tests.
   - Singular naming is required: `.Test`, never `.Tests`.
5. Run `Apply` only after reviewing the `Analyze` output.
6. After `Apply`, run restore and full solution tests.

## Command examples

Analyze the current repo:

```powershell
powershell -ExecutionPolicy Bypass -File {{skill_dir}}\umbrella-dotnet-standardize-test-projects\scripts\Invoke-StandardizeTestProjects.ps1 -Mode Analyze
```

Apply changes:

```powershell
powershell -ExecutionPolicy Bypass -File {{skill_dir}}\umbrella-dotnet-standardize-test-projects\scripts\Invoke-StandardizeTestProjects.ps1 -Mode Apply
```

Analyze another repo:

```powershell
powershell -ExecutionPolicy Bypass -File {{skill_dir}}\umbrella-dotnet-standardize-test-projects\scripts\Invoke-StandardizeTestProjects.ps1 -Mode Analyze -RepoRoot "D:\ProjectsGIT\BOLE\ProjectBOLE-Kernel"
```

## Standard Pattern

Central files should provide:

- `global.json`: `"test": { "runner": "Microsoft.Testing.Platform" }` so .NET 10+ `dotnet test` uses the Microsoft Testing Platform runner path.
- `Directory.Build.props`: `<Using Include="Xunit" />` in an `ItemGroup` conditioned on `IsTestProject=true`.
- `Directory.Build.targets`: test-only `OutputType`, `IsPackable`, `NoWarn`, `WarningsAsErrors`, `PreserveCompilationContext`, and `UseMicrosoftTestingPlatformRunner`.
- `Directory.Packages.props`: shared test package injection for `Microsoft.Testing.Extensions.CodeCoverage`, `Microsoft.Testing.Extensions.TrxReport`, `Moq`, and `xunit.v3.mtp-v2`. The script treats its versions as minimum baselines: it upgrades older explicit versions and preserves newer compatible explicit versions.

Runnable test projects should keep only project-specific configuration:

- `<IsTestProject>true</IsTestProject>`
- local `TargetFramework` only when existing runnable tests declare it locally; omit it when inherited from `Directory.Build.props`
- project-specific package references and project references
- project-specific warning suppressions, without duplicating central `CS1591` or `xUnit1051`

Helper projects under test folders should not be runnable tests. Use `<IsTestProject>false</IsTestProject>` for shared/mocks/support projects and keep only packages they genuinely need.

## Safety Rules

- Do not rename `.Tests` projects automatically. Report plural naming drift so the caller can perform a deliberate project/file/solution rename.
- Do not remove project-specific packages such as `Xunit.v3.Priority`, `Microsoft.AspNetCore.Mvc.Testing`, Testcontainers packages, user-secrets packages, or provider-specific test dependencies.
- Preserve additional repository-specific `NoWarn` and `WarningsAsErrors` entries in the central test-only `Directory.Build.targets` block while enforcing the shared baseline entries.
- Never downgrade a newer explicit shared test-package version merely to match the script baseline.
- Do not run `Apply` in a dirty repo without first reviewing unrelated user changes.
- After `Apply`, run:

```powershell
dotnet restore "<SolutionFile>"
dotnet test "<SolutionFile>" --no-restore --verbosity minimal
```

Do not add legacy VSTest `--logger` arguments to Microsoft Testing Platform invocations. Use the reporting options exposed by the installed MTP extensions and confirm them with `dotnet test --help`.

## Analyzer compatibility

Before finishing, read `.ai-shared\bundles\umbrella\analyzer-compatibility.md` and build the affected projects with their installed analyzers enabled. Treat diagnostics introduced by the generated or changed code as defects in this workflow.

## Output

The script prints a summary and writes a JSON report with:

- `centralFiles`
- `projects`
- `drift`
- `changedFiles`
- `warnings`
