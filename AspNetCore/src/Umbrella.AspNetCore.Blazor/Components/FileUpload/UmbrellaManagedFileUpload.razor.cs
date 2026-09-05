using Microsoft.Extensions.Logging;
using Umbrella.AspNetCore.Blazor.Components.Dialog.Abstractions;
using Umbrella.Utilities.Primitives.Abstractions;

namespace Umbrella.AspNetCore.Blazor.Components.FileUpload;

/// <summary>
/// A component that manages an existing file and delegates new file selection and uploading to <see cref="UmbrellaFileUpload"/>.
/// </summary>
/// <seealso cref="ComponentBase" />
public partial class UmbrellaManagedFileUpload : ComponentBase
{
	private bool _parametersInitialized;
	private bool _isReplacingExistingFile;
	private string? _lastExistingFileUrl;

	[Inject]
	private ILogger<UmbrellaManagedFileUpload> Logger { get; set; } = null!;

	[Inject]
	private IUmbrellaDialogService DialogUtility { get; set; } = null!;

	/// <summary>
	/// Gets or sets the URL of the existing file. When specified, the existing-file actions are displayed initially.
	/// </summary>
	[Parameter]
	public string? ExistingFileUrl { get; set; }

	/// <summary>
	/// Gets or sets the maximum file size in bytes that can be uploaded.
	/// </summary>
	/// <remarks>Defaults to 512000 bytes.</remarks>
	[Parameter]
	public int? MaxFileSizeBytes { get; set; } = 512000;

	/// <summary>
	/// Gets or sets whether a warning message should be shown when the current file selection is cleared.
	/// </summary>
	[Parameter]
	public bool ShowClearWarning { get; set; } = true;

	/// <summary>
	/// Gets or sets whether a warning message should be shown when an upload is cancelled.
	/// </summary>
	[Parameter]
	public bool ShowCancelWarning { get; set; } = true;

	/// <summary>
	/// Gets or sets whether a warning message should be shown before the existing file is removed.
	/// </summary>
	[Parameter]
	public bool ShowRemoveWarning { get; set; } = true;

	/// <summary>
	/// Gets or sets a comma-delimited list of file extensions and/or MIME types that this component will accept.
	/// </summary>
	[Parameter]
	public string? Accept { get; set; }

	/// <summary>
	/// Gets or sets the delegate invoked when the Upload button is clicked.
	/// </summary>
	[Parameter]
	[EditorRequired]
	public Func<UmbrellaFileUploadRequestEventArgs, Task<IOperationResult?>>? OnRequestUpload { get; set; }

	/// <summary>
	/// Gets or sets the callback invoked before the component switches from the existing file to replacement mode.
	/// </summary>
	[Parameter]
	public EventCallback OnReplaceFile { get; set; }

	/// <summary>
	/// Gets or sets the callback invoked when the existing file is removed.
	/// </summary>
	/// <remarks>The Remove button is only rendered when this callback has a delegate.</remarks>
	[Parameter]
	public EventCallback OnRemoveFile { get; set; }

	/// <summary>Gets or sets whether the View button is displayed.</summary>
	[Parameter]
	public bool ShowViewButton { get; set; } = true;

	/// <summary>Gets or sets whether the Replace button is displayed.</summary>
	[Parameter]
	public bool ShowReplaceButton { get; set; } = true;

	/// <summary>Gets or sets whether the Remove button is displayed when <see cref="OnRemoveFile"/> has a delegate.</summary>
	[Parameter]
	public bool ShowRemoveButton { get; set; } = true;

	/// <summary>Gets or sets whether the existing file is opened in a new browser window or tab.</summary>
	[Parameter]
	public bool OpenExistingFileInNewWindow { get; set; } = true;

	/// <summary>Gets or sets the accessible label for the existing-file action group.</summary>
	[Parameter]
	public string ExistingFileActionsAriaLabel { get; set; } = "Existing file actions";

	/// <summary>Gets or sets the View button text.</summary>
	[Parameter]
	public string ViewButtonText { get; set; } = "View File";

	/// <summary>Gets or sets the Replace button text.</summary>
	[Parameter]
	public string ReplaceButtonText { get; set; } = "Replace";

	/// <summary>Gets or sets the Remove button text.</summary>
	[Parameter]
	public string RemoveButtonText { get; set; } = "Remove";

	/// <summary>Gets or sets the CSS classes applied to the View button.</summary>
	[Parameter]
	public string ViewButtonCssClass { get; set; } = "btn btn-primary";

	/// <summary>Gets or sets the CSS classes applied to the Replace button.</summary>
	[Parameter]
	public string ReplaceButtonCssClass { get; set; } = "btn btn-secondary";

	/// <summary>Gets or sets the CSS classes applied to the Remove button.</summary>
	[Parameter]
	public string RemoveButtonCssClass { get; set; } = "btn btn-danger";

	/// <summary>Gets or sets the confirmation message shown before removing the existing file.</summary>
	[Parameter]
	public string RemoveWarningMessage { get; set; } = "Are you sure you want to remove the existing file?";

	/// <summary>Gets or sets the title of the confirmation shown before removing the existing file.</summary>
	[Parameter]
	public string RemoveWarningTitle { get; set; } = "Remove File";

	internal bool IsShowingExistingFile { get; private set; }

	/// <inheritdoc />
	protected override void OnParametersSet() => SynchronizeExistingFile();

	internal void SynchronizeExistingFile()
	{
		if (!_parametersInitialized || !string.Equals(ExistingFileUrl, _lastExistingFileUrl, StringComparison.Ordinal))
		{
			IsShowingExistingFile = !string.IsNullOrWhiteSpace(ExistingFileUrl);
			_isReplacingExistingFile = false;
		}

		_parametersInitialized = true;
		_lastExistingFileUrl = ExistingFileUrl;
	}

	internal async Task ReplaceFileClickAsync()
	{
		try
		{
			if (OnReplaceFile.HasDelegate)
				await OnReplaceFile.InvokeAsync();

			_isReplacingExistingFile = IsShowingExistingFile;
			IsShowingExistingFile = false;
		}
		catch (Exception exc) when (Logger.WriteError(exc))
		{
			_isReplacingExistingFile = false;
			await DialogUtility.ShowDangerMessageAsync();
		}
	}

	internal async Task<IOperationResult?> OnRequestUploadInnerAsync(UmbrellaFileUploadRequestEventArgs args)
	{
		if (OnRequestUpload is null)
			throw new InvalidOperationException($"The {nameof(OnRequestUpload)} property does not have an assigned delegate.");

		IOperationResult? result = await OnRequestUpload(args);

		if (RestoreExistingFileAfterSuccessfulUpload(result))
			await InvokeAsync(StateHasChanged);

		return result;
	}

	internal bool RestoreExistingFileAfterSuccessfulUpload(IOperationResult? result)
	{
		if (result is not { IsSuccess: true } || !_isReplacingExistingFile || string.IsNullOrWhiteSpace(ExistingFileUrl))
			return false;

		IsShowingExistingFile = true;
		_isReplacingExistingFile = false;

		return true;
	}

	internal async Task RemoveFileClickAsync()
	{
		try
		{
			if (ShowRemoveWarning)
			{
				bool remove = await DialogUtility.ShowConfirmDangerMessageAsync(RemoveWarningMessage, RemoveWarningTitle, RemoveButtonText);

				if (!remove)
					return;
			}

			if (OnRemoveFile.HasDelegate)
				await OnRemoveFile.InvokeAsync();

			_isReplacingExistingFile = false;
			IsShowingExistingFile = false;
		}
		catch (Exception exc) when (Logger.WriteError(exc))
		{
			await DialogUtility.ShowDangerMessageAsync();
		}
	}
}
