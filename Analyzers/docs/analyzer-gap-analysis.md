# Umbrella Analyzer Gap Analysis

**Date:** May 2026  
**Reference codebase:** Thrive For Send (`D:\ProjectsGIT\Thrive For Send`)  
**Purpose:** Record of the gap analysis performed before building out AI agent skills, and the current status of all identified issues.

---

## Current Rule Inventory (`Umbrella.Analyzers`)

| ID | Rule | Category |
|----|------|----------|
| UA001 | Use `is null` / `is not null` instead of `== null` | CodeStyle |
| UA002 | Use `is` / `is not` for primitive and enum comparisons | CodeStyle |
| UA003 | Async methods must have a `CancellationToken` parameter | CodeStyle |
| UA004 | Async methods must call `ThrowIfCancellationRequested()` as first statement | CodeStyle |
| UA005 | Collection parameters must use `IEnumerable<T>` | CodeStyle |
| UA006 | Collection return types must use `IReadOnlyCollection<T>` | CodeStyle |
| UA007 | Collection return types must be non-nullable | CodeStyle |
| UA008 | Public methods must be wrapped in try-catch with logging | CodeStyle |
| UA009 | Parameter validation must appear before try-catch | CodeStyle |
| UA010 | No primary constructors on non-record classes/structs | CodeStyle |
| UA011 | Model/QueryResult types must be records | UmbrellaModelStandards |
| UA012 | Model properties must use `required` | UmbrellaModelStandards |
| UA013 | Model properties must use `{ get; init; }` | UmbrellaModelStandards |
| UA014 | Model collection properties must use `IReadOnlyCollection<T>` | UmbrellaModelStandards |

---

## Resolved Issues

### UA008 — False-positive on exception-filter logging pattern ✅ Fixed

**Was:** `ContainsLoggingStatement()` only checked `catchClause.Block.Statements`, missing the Umbrella
pattern `catch (Exception exc) when (Logger.WriteError(exc, ...))` where logging is in the `when` filter.  
**Fix:** Extended to also walk `catchClause.Filter` via `ContainsLoggerInvocationInExpression`, which
recursively handles binary (`&&`/`||`) and parenthesised filter expressions.

### UA008 — Expression-bodied public methods silently skipped ✅ Fixed

**Was:** Early return on `methodDeclaration.Body is null` meant expression-bodied methods (`=> expr`)
were never checked.  
**Fix:** Split the condition — expression-bodied methods now trigger the diagnostic immediately; abstract/
extern/partial stubs (no body and no expression body) continue to be skipped.

### UA005 — False-positive on `params` parameters ✅ Fixed

**Was:** No `params` exclusion, so `params int[] ids` was flagged as needing `IEnumerable<int>` even
though `params` requires an array type.  
**Fix:** Added `parameter.IsParams` guard before the collection-type check.

### UMS003 — Operator precedence made opt-out logic unclear ✅ Fixed

**Was:** A single compound boolean condition with mixed `&&`/`||` made the intent ambiguous. In practice
the behaviour was correct (`[UmbrellaAllowMutableProperty]` suppressed only the setter check, not the
missing-getter check), but the code did not make that obvious.  
**Fix:** Split into two named booleans (`hasMissingGetter`, `hasSetterWithoutInit`) to make the intent
explicit. Semantics unchanged; a new test documents the getter requirement is not suppressable.

### UA011–UA014 — Model standards not covering `*QueryResult` types ✅ Fixed

**Was:** `IsModelType()` only matched `*Model`, `*ModelBase`, `*ViewModel`, `*ViewModelBase`.  
**Fix:** Added `*QueryResult` to the suffix list. Every `*QueryResult` type in Thrive For Send already
follows the record/required/init-only/IReadOnlyCollection convention; the analyzer now enforces it.

### UA011–UA014 — Lived in a separate `Umbrella.Analyzers.ModelStandards` package ✅ Merged

**Was:** Model standards shipped as a separate NuGet package requiring a second package reference,
separate versioning, and a separate test project.  
**Fix:** Merged into `Umbrella.Analyzers`. Diagnostic IDs renamed from `UMS001–UMS004` to `UA011–UA014`.
The old project and test project have been deleted.

---

## Deferred to `Microsoft.VisualStudio.Threading.Analyzers`

The following gaps were identified but not implemented because the Threading.Analyzers package already
covers them:

| Gap | Covered by |
|-----|------------|
| `Async` suffix on Task/ValueTask methods | VSTHRD200 |
| `internal sealed` on concrete implementation classes | Threading.Analyzers |
| No `.Result` / `.Wait()` on Task | VSTHRD002 |

---

## Remaining Known Limitation

**UMS code fix provider disabled** — `UmbrellaModelStandardsCodeFixProvider.cs` (now removed with the
merge) was entirely commented out due to a `netstandard2.0` vs `net8` targeting conflict. The rules fire
as errors so the guardrail is in place, but no automatic IDE fix is offered. Re-enable when the analyzer
package targeting is revisited.

---

## Project-Level Patterns (enforce via skill instructions, not Roslyn rules)

These are Thrive For Send / Umbrella consumer conventions that are too project-specific to encode in
`Umbrella.Analyzers`. They should be enforced via the skill prompt instructions in each
`umbrella.*` skill bundle.

| Pattern | Enforce in skill |
|---------|-----------------|
| `Lazy<T>` for all constructor-injected dependencies | `blazor-scaffold-repository`, `blazor-scaffold-service` |
| Layer-specific exception types (`ThriveForSend[Layer]Exception`) | All scaffolding skills |
| `IncludeMap<T>` pattern for EF eager loading | `blazor-scaffold-repository` |
| Mapperly `[Entity]Mapper` per entity crossing the API boundary | `blazor-scaffold-server-models` |

---

## Proposed New Analyzer Rules

Identified during skill audit (May 2026). These are candidates for `Umbrella.Analyzers` — not yet implemented.

| Proposed ID | Rule | Severity | Category | Trigger |
|---|---|---|---|---|
| UA015 | API model records must be `public partial record` | Warning | UmbrellaModelStandards | Non-partial record found in `Web.Shared\Models\Api\` |
| UA016 | Input model records with string properties must implement `IUmbrellaTrimmable` (Blazor projects only) | Warning | UmbrellaModelStandards | String property on a `Create*` or `Update*` record that does not implement `IUmbrellaTrimmable` |
| UA017 | Controllers inheriting `UmbrellaApiController` must use `[UmbrellaProducesResponseType]`, not `[ProducesResponseType]` | Warning | UmbrellaApiStandards | `[ProducesResponseType]` attribute found on a class or method within the `UmbrellaApiController` inheritance chain |
| UA018 | `IAuthorizationHandler` implementations must not call `context.Fail()` | Error | UmbrellaSecurity | `context.Fail()` call found inside `HandleRequirementAsync` — explicit failure blocks all other handlers, violating ASP.NET Core convention |
| UA019 | Mapperly mapper classes must be `public partial class` | Warning | UmbrellaMapperStandards | Mapper class in a ModelFactories assembly that is not `public` or not `partial` — prevents `UmbrellaMapper` assembly scan from discovering it |

**Implementation notes:**
- UA015 complements UA011 (which enforces `record`, not `partial record`). Consider extending UA011 rather than adding a separate rule.
- UA016 is Blazor-project-scoped — gate on project type (presence of `Blazor.WebAssembly` or `Blazor.Server` package reference) to avoid false positives in standalone API projects.
- UA017 should suppress on any method/class already decorated with `[UmbrellaProducesResponseType]`.
- UA018 — check whether any existing rule (e.g. a security-focused custom rule) already covers `context.Fail()` before implementing.
- UA019 — scope to assemblies named `*.ModelFactories` or containing Mapperly-annotated classes.
