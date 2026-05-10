---
name: blazor-scaffold-manage-page
description: 'Scaffold a Blazor manage page (.razor + .razor.cs) for create/edit operations, following the Umbrella EditForm pattern with UmbrellaModelLayoutStateView, breadcrumb, auth policy, and concurrency handling.'
---

# Scaffold Blazor Manage Page

## Purpose

Add a Blazor manage page that handles both create and edit for a feature. The page uses `EditForm` with `UmbrellaModelLayoutStateView` to manage loading states, and dispatches to `Repository.CreateAsync` or `Repository.UpdateAsync` based on whether an ID parameter is present.

**Prerequisite:** A client data service interface (`I<Name>Service`) must exist with `CreateAsync` and `UpdateAsync` methods (i.e. the service interface extends `IGenericDataService` with non-NoOp create/update model types). The create model, update model, and their shared base (`CreateUpdate<Name>ModelBase`) must exist.

## Discovery (read these before writing anything)

1. Read 2–3 existing manage pages under `Web\<AppName>.Web.Client\Pages\Admin\` to understand the form field patterns, file upload handling if applicable, and navigation after save.
2. Confirm the project-specific client component base class name (e.g. `ThriveForSendClientComponentBase`).
3. Read `Web\<AppName>.Web.Shared\Security\Policies\<AppName>PolicyNames.cs` or `SharedPolicyNames.cs` for the correct auth policy constant.
4. Read the create/update model types for the feature to know which properties to include as form fields.

---

## Step 1 -- Create Manage.razor

**File:** `Web\<AppName>.Web.Client\Pages\Admin\<Name>Management\Manage.razor`

```razor
@inherits ManageBase
@page "/admin/<route-plural>/manage"
@page "/admin/<route-plural>/manage/{Id:int}"

@{
    string title = "Manage <Names>";
    string subTitle = Id.HasValue ? "Edit" : "Create";
}

<ThriveForSendPageTitle>@title</ThriveForSendPageTitle>

<UmbrellaBreadcrumb>
    <UmbrellaBreadcrumbItem Name="@title" Url="/admin/<route-plural>" />
    <UmbrellaBreadcrumbItem Name="@subTitle" />
</UmbrellaBreadcrumb>

<div class="management-page">
    <h1>@title</h1>
    <h4>@subTitle</h4>
    <hr />
    <section>
        <UmbrellaModelLayoutStateView CurrentState="CurrentState" Model="CreateUpdateModel" ReloadCallback="ReloadAsync">
            <Success>
                <UmbrellaValidationSummary ValidationResults="ValidationResults" />
                <EditForm Model="CreateUpdateModel" OnValidSubmit="SubmitFormAsync" novalidate>
                    <ObjectGraphDataAnnotationsValidator />

                    <div class="form-group form-floating">
                        <UmbrellaInputText class="form-control" @bind-Value="CreateUpdateModel!.Name" />
                        <LabelText ForTarget="() => CreateUpdateModel!.Name" />
                        <ValidationMessage For="() => CreateUpdateModel.Name" />
                    </div>

                    <div class="form-group form-group--buttons">
                        <button type="submit" class="btn btn-primary">@(Id.HasValue ? "Save Changes" : "Create <Name>")</button>
                        <a href="/admin/<route-plural>" class="btn btn-secondary">Cancel</a>
                    </div>
                </EditForm>
            </Success>
        </UmbrellaModelLayoutStateView>
    </section>
</div>
```

**Rules:**
- Two `@page` directives: the create route (no ID) and the edit route (`{Id:int}`).
- `@inherits ManageBase` only — no C# logic in the `.razor` beyond the `@{...}` title block.
- `UmbrellaModelLayoutStateView` wraps the form — always present; it handles the loading/error/success state machine.
- `CurrentState` and `ReloadCallback` come from the base class — do not define them.
- Form fields: add one `<div class="form-group">` per editable property. Use `form-floating` for text inputs. Check existing manage pages for the right component per field type (`UmbrellaInputText`, `UmbrellaInputTextArea`, `UmbrellaInputSelect`, etc.).
- Cancel button always links back to the index route.
- Submit button label changes based on `Id.HasValue`.

### File upload field pattern

When the feature includes image/file upload:

```razor
<div class="form-group">
    <LabelText ForTarget="() => CreateUpdateModel.<FilePropertyName>" />
    <UmbrellaFileImagePreviewUpload @ref="ImagePreviewUpload"
                                    OnRequestUpload="UploadFileToTempDirectoryAsync"
                                    OnDeleteImage="OnDeleteImage"
                                    Accept="@GlobalFileSystemConstants.<Name>FileExtensions"
                                    MaxFileSizeBytes="@GlobalFileSystemConstants.<Name>MaxSizeBytes"
                                    WidthRequest="400"
                                    HeightRequest="400"
                                    Url="@Model?.ImageUrl" />
    <ValidationMessage For="() => CreateUpdateModel.<FilePropertyName>" />
</div>
```

---

## Step 2 -- Create Manage.razor.cs

**File:** `Web\<AppName>.Web.Client\Pages\Admin\<Name>Management\Manage.razor.cs`

```csharp
using <AppName>.Web.Client.Data.Services.Abstractions;
using <AppName>.Web.Shared.Models.Api.<Feature>;

namespace <AppName>.Web.Client.Pages.Admin.<Name>Management;

[Authorize(<AppName>PolicyNames.<Policy>)]
public abstract class ManageBase : <AppName>ClientComponentBase
{
    [Parameter]
    public int? Id { get; set; }

    [Inject]
    private I<Name>Service Repository { get; set; } = null!;

    protected <Name>Model? Model { get; private set; }
    protected CreateUpdate<Name>ModelBase? CreateUpdateModel { get; private set; }

    protected IReadOnlyCollection<ValidationResult>? ValidationResults { get; private set; }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            if (!Id.HasValue)
            {
                CreateUpdateModel = new Create<Name>Model();
                CurrentState = LayoutState.Success;

                return;
            }

            var result = await Repository.FindByIdAsync(Id.Value);

            if (result.IsSuccess && result.Result != null)
            {
                Model = result.Result;

                CreateUpdateModel = new Update<Name>Model
                {
                    Id = result.Result.Id,
                    ConcurrencyStamp = result.Result.ConcurrencyStamp,
                    // map remaining properties from result.Result
                };

                CurrentState = LayoutState.Success;

                return;
            }
            else
            {
                await ShowOperationResultErrorMessageAsync(result);
            }
        }
        catch (Exception exc) when (Logger.WriteError(exc, new { Id }))
        {
            await DialogUtility.ShowDangerMessageAsync();
        }

        CurrentState = LayoutState.Error;
    }

    protected async Task SubmitFormAsync()
    {
        try
        {
            if (CreateUpdateModel is null)
                throw new InvalidOperationException("The CU model is null");

            if (Id.HasValue && Model is null)
                throw new InvalidOperationException("The model is null");

            if (CreateUpdateModel is Create<Name>Model createModel)
            {
                var result = await Repository.CreateAsync(createModel);

                if (result.IsSuccess)
                {
                    await DialogUtility.ShowSuccessMessageAsync("The <Name> has been created successfully.");
                    Navigation.NavigateTo("/admin/<route-plural>");
                }
                else
                {
                    await ShowOperationResultErrorMessageAsync(result);
                }
            }
            else if (CreateUpdateModel is Update<Name>Model updateModel)
            {
                var result = await Repository.UpdateAsync(updateModel);

                if (result.IsSuccess && result.Result is not null)
                {
                    await DialogUtility.ShowSuccessMessageAsync("The <Name> has been updated successfully.");
                    await ReloadAsync();
                }
                else
                {
                    await ShowOperationResultErrorMessageAsync(result);
                }
            }
        }
        catch (UmbrellaConcurrencyException)
        {
            await DialogUtility.ShowDangerMessageAsync(ClientErrorMessages.Concurrency);
        }
        catch (Exception exc) when (Logger.WriteError(exc, new { Model, CreateUpdateModel }))
        {
            await DialogUtility.ShowDangerMessageAsync();
        }
    }
}
```

**Rules:**
- `public abstract class` — the `.razor` file inherits from it via `@inherits`.
- `[Authorize(PolicyName)]` on the class, not in the `.razor` file.
- `[Inject] private I<Name>Service Repository { get; set; } = null!;` — the property is always named `Repository` by convention, regardless of the type name.
- `CreateUpdateModel` is typed as `CreateUpdate<Name>ModelBase?` (the abstract base), so both create and update models can be assigned to it.
- `OnInitializedAsync`: if no `Id`, construct an empty `Create<Name>Model` and set `CurrentState = LayoutState.Success`. If `Id` is set, load from `Repository.FindByIdAsync` and map into an `Update<Name>Model`. Always set `CurrentState = LayoutState.Error` in the final catch/fallthrough.
- `SubmitFormAsync`: pattern-match on `CreateUpdateModel` type to call the correct method. After a successful update, call `ReloadAsync()` (full page reload from base class) rather than navigating away — this ensures the latest concurrency stamp is reflected. After a successful create, navigate to the index route.
- Concurrency exception is caught specifically with a dedicated user message.
- `System.ComponentModel.DataAnnotations` is needed if `ValidationResult` is referenced — add the `using` if required.

### Additional injections for file upload

When file upload is needed, add:

```csharp
[Inject]
private IFileUploadService FileUploadService { get; set; } = null!;

protected UmbrellaFileImagePreviewUpload ImagePreviewUpload { get; set; } = null!;

public async Task<IOperationResult?> UploadFileToTempDirectoryAsync(UmbrellaFileUploadRequestEventArgs evt)
{
    try
    {
        if (CreateUpdateModel is null)
            throw new InvalidOperationException("The model should not be null here.");

        var fileUploadResult = await FileUploadService.UploadAsync(evt.Content, evt.FileName, FileUploadType.<Name>Image, evt.Type, CancellationToken);

        if (fileUploadResult.IsSuccess)
        {
            CreateUpdateModel.<FilePropertyName> = fileUploadResult.Result.tempFileName;
            ImagePreviewUpload.Update(fileUploadResult.Result.url);
        }

        StateHasChanged();

        return fileUploadResult;
    }
    catch (Exception exc) when (Logger.WriteError(exc))
    {
        await DialogUtility.ShowDangerMessageAsync();
    }

    return null;
}

protected void OnDeleteImage()
{
    if (CreateUpdateModel is null)
        throw new InvalidOperationException("The model should not be null here.");

    if (CreateUpdateModel is Update<Name>Model updateModel)
        updateModel.ReplaceExistingImage = true;
}
```

---

## Verification

1. Two `@page` directives — create route (no ID) and edit route (`{Id:int}`).
2. `[Authorize(PolicyName)]` is on the code-behind class, not in the `.razor` file.
3. `[Inject]` property is named `Repository` and typed as the service interface.
4. `CreateUpdateModel` is typed as the abstract base (`CreateUpdate<Name>ModelBase?`), not as create or update directly.
5. `OnInitializedAsync` sets `CurrentState = LayoutState.Success` on both the create and edit success paths, and `LayoutState.Error` on failure.
6. `SubmitFormAsync` pattern-matches on `Create<Name>Model` vs `Update<Name>Model` — navigates after create, reloads after update.
7. `UmbrellaConcurrencyException` is caught and handled with `ClientErrorMessages.Concurrency`.
8. `UmbrellaModelLayoutStateView` wraps the form content in the `.razor`.
