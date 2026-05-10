## Umbrella Skills

The following skills are available in `.claude\skills\`. Read a skill's `SKILL.md` for full instructions before using it.

- `nuget-safe-upgrade` -- upgrade NuGet packages safely across multiple TFMs; use the `nuget-safe-upgrade` agent
- `dotnet-add-ef-migration` -- add a new EF Core migration
- `dotnet-scaffold-ef-entity` -- add a new EF Core entity class and register it in DbContext
- `dotnet-scaffold-ef-repository` -- add a repository interface and implementation for an existing EF Core entity
- `dotnet-scaffold-service` -- add a logic service to the Core.Logic project
- `dotnet-scaffold-file-handler` -- add a file handler to the Core.Logic FileSystem project
- `dotnet-scaffold-file-authorization-handler` -- add a file authorization handler to the Core.Logic FileSystem project
- `dotnet-scaffold-api-server-models` -- add API model records (request/response types) for a feature
- `dotnet-scaffold-mapperly-factories` -- add Mapperly mapper classes that map between entities and API models
- `dotnet-scaffold-api-repo-controller` -- add an API controller that communicates directly with a repository (GenericRepositoryApiController pattern)
- `dotnet-scaffold-api-data-service-controller` -- add a thin API controller backed by a controller service, with a shared service interface that supports Blazor SSR pre-rendering (GenericRepositoryDataServiceApiController pattern)
- `dotnet-scaffold-client-data` -- add a client-side HTTP data service implementing an existing IManage<Name>Service interface (GenericHttpDataService pattern); updates server DI to ReplaceScoped
- `dotnet-rename-client-repository-to-service` -- rename a client data type from the legacy ...Repository convention to ...Service, moving files, updating namespaces, DI, and all Blazor component references
- `dotnet-migrate-repo-controller-to-data-service` -- migrate an existing GenericRepositoryApiController (Pattern 1) to GenericRepositoryDataServiceApiController (Pattern 2), creating the backing controller service and rewiring DI
- `blazor-scaffold-index-page` -- add a Blazor index/listing page with UmbrellaGrid, breadcrumb, auth policy, and action column
- `blazor-scaffold-manage-page` -- add a Blazor create/edit manage page with EditForm, UmbrellaModelLayoutStateView, auth policy, and concurrency handling
- `blazor-register-nav-item` -- add a nav item to NavMenu.razor inside the correct AuthorizeView policy block
- `dotnet-scaffold-auth-policy` -- add a named authorization policy constant and register it in the shared AuthorizationOptions extension method
- `dotnet-scaffold-resource-auth-handler` -- add a resource-based IAuthorizationHandler for row-level access control on a specific entity type
