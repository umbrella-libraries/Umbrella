## Umbrella Blazor Scaffold Entity

When creating a new EF Core entity in a BlazorWasm project, use the `blazor-scaffold-entity` skill.

Read `.claude\skills\blazor-scaffold-entity\SKILL.md` for full instructions.

Key rules:
- Entity classes go in `Core\<AppName>.Core.Domain\Entities\`
- All entity classes are `public partial class`
- Register in DbContext via a private static `Add<EntityName>` method — never add a `DbSet<T>` property
- Every string property needs a length attribute (`[ShortStringLength]`, `[MediumStringLength]`, `[LongStringLength]`)
- Every FK column needs an index in the configuration method
