
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;
using Umbrella.AspNetCore.Blazor.Components.Dialog.Abstractions;
using Umbrella.Utilities.Primitives.Abstractions;

namespace Umbrella.AspNetCore.Blazor.Components.FileUpload;

/// <summary>
/// A component that can be used to upload files that wraps the built-in <see cref="InputFile"/> component with support for upload progress
/// and providing the user with options to upload or clear the current file selection.
/// </summary>
/// <seealso cref="ComponentBase" />
/// <seealso cref="IDisposable" />
public partial class UmbrellaFileUpload : ComponentBase, IDisposable
{
	private CancellationTokenSource? _cancellationTokenSource;

	[Inject]
	private ILogger<UmbrellaFileUpload> Logger { get; set; } = null!;

	[Inject]
	private IUmbrellaDialogService DialogUtility { get; set; } = null!;

	private int UploadPercentage { get; set; }

	/// <summary>
	/// Gets or sets the maximum file size in bytes that can be uploaded.
	/// </summary>
	/// <remarks>
	/// Defaults to 512000 bytes.
	/// </remarks>
	[Parameter]
	public int? MaxFileSizeBytes { get; set; } = 512000;

	/// <summary>
	/// Gets or sets whether a warning message should be shown to the user when they clear the current file selection.
	/// </summary>
	[Parameter]
	public bool ShowClearWarning { get; set; } = true;

	/// <summary>
	/// Gets or sets whether a warning message should be shown to the user when they cancel the file upload.
	/// </summary>
	[Parameter]
	public bool ShowCancelWarning { get; set; } = true;

	/// <summary>
	/// Gets or sets a comma-delimited list of file extensions and/or MIME types that this component will accept.
	/// </summary>
	[Parameter]
	public string? Accept { get; set; }

	/// <summary>
	/// Gets or sets the delegate that is invoked when the Upload button is clicked.
	/// </summary>
	[Parameter]
	[EditorRequired]
	public Func<UmbrellaFileUploadRequestEventArgs, Task<IOperationResult?>>? OnRequestUpload { get; set; }

	private string Id { get; } = Guid.NewGuid().ToString();
	private IBrowserFile? SelectedFile { get; set; }
	private UmbrellaFileUploadStatus Status { get; set; }
	private string? AcceptTypesMessage { get; set; }
	private int? MaxFileSizeMegaBytes { get; set; }

	/// <inheritdoc />
	protected override async Task OnParametersSetAsync()
	{
		try
		{
			if (MaxFileSizeBytes.HasValue)
				MaxFileSizeMegaBytes = MaxFileSizeBytes / 1024 / 1024;

			if (!string.IsNullOrWhiteSpace(Accept))
			{
				string[] types = Accept.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => x.TrimToLowerInvariant()).Distinct().ToArray();

				if (types.Length is 0)
				{
					Accept = null;
					return;
				}

				// NB: The types[..^1] was originally types.SkipLast(1)
				// The types[^1] was originally types[types.Length - 1]. The syntax is using the new C#8 Index stuff. ^1 effectively means get the item at the end.
				AcceptTypesMessage = types.Length is 1
					? $"Please select a {types[0]} file to upload."
					: $"Please select a {string.Join(", ", types[..^1])} or a {types[^1]} file to upload.";
			}
		}
		catch (Exception exc) when (Logger.WriteError(exc))
		{
			await DialogUtility.ShowDangerMessageAsync();
		}
	}

	private async Task OnFileSelectedAsync(InputFileChangeEventArgs args)
	{
		try
		{
			SelectedFile = args.File;

			if (SelectedFile is not null)
			{
				Status = UmbrellaFileUploadStatus.Selected;

				if (SelectedFile.Size > MaxFileSizeBytes)
				{
					await DialogUtility.ShowDangerMessageAsync($"Please select a file with a maximum size of {MaxFileSizeMegaBytes} MB.", "Maximum File Size Exceeded");
					return;
				}

				return;
			}

			Status = UmbrellaFileUploadStatus.None;
		}
		catch (Exception exc) when (Logger.WriteError(exc))
		{
			await DialogUtility.ShowDangerMessageAsync();
		}
	}

	private async Task UploadClickAsync()
	{
		try
		{
			UploadPercentage = 0;

			if (SelectedFile is null)
				throw new InvalidOperationException("File Info should not be null here.");

			if (OnRequestUpload is null)
				throw new InvalidOperationException($"The {OnRequestUpload} property must have an assigned delegate.");

			_cancellationTokenSource = new CancellationTokenSource();
			Status = UmbrellaFileUploadStatus.Uploading;

			using Stream stream = SelectedFile.OpenReadStream(MaxFileSizeBytes ?? 512000, _cancellationTokenSource.Token);

			await using Timer timer = new(_ => _ = InvokeAsync(() =>
			{
				UploadPercentage = (int)Math.Round(stream.Position / stream.Length * 100d, MidpointRounding.AwayFromZero);
				StateHasChanged();
			}),
			null,
			TimeSpan.FromMilliseconds(500),
			TimeSpan.FromMilliseconds(500));

			IOperationResult? result = await OnRequestUpload(new UmbrellaFileUploadRequestEventArgs(stream, SelectedFile.Name, SelectedFile.ContentType, _cancellationTokenSource.Token));

			if (result is { IsSuccess: true })
			{
				Cleanup(UmbrellaFileUploadStatus.Uploaded);
				await DialogUtility.ShowSuccessMessageAsync("Your file has been uploaded successfully.");
			}
			else
			{
				Cleanup(UmbrellaFileUploadStatus.Selected);
				await DialogUtility.ShowOperationResultErrorMessageAsync(result);
			}
		}
		catch (Exception exc) when (Logger.WriteError(exc))
		{
			Cleanup(UmbrellaFileUploadStatus.Selected);
			await DialogUtility.ShowDangerMessageAsync("There has been a problem uploading the selected file. Please try again.");
		}
	}

	private async Task ClearClickAsync()
	{
		try
		{
			if (ShowClearWarning)
			{
				bool clear = await DialogUtility.ShowConfirmWarningMessageAsync("Are you sure you want to clear the current file and choose a new one?", "Clear Selection");

				if (!clear)
					return;
			}

			ClearSelection();
		}
		catch (Exception exc) when (Logger.WriteError(exc))
		{
			await DialogUtility.ShowDangerMessageAsync();
		}
	}

	private async Task CancelClickAsync()
	{
		try
		{
			if (ShowCancelWarning)
			{
				bool cancel = await DialogUtility.ShowConfirmWarningMessageAsync("Are you sure you want to cancel the file upload?", "Cancel Upload");

				if (!cancel)
					return;
			}

			Cleanup(UmbrellaFileUploadStatus.Selected);
		}
		catch (Exception exc) when (Logger.WriteError(exc))
		{
			await DialogUtility.ShowDangerMessageAsync();
		}
	}

	/// <inheritdoc />
	public void Dispose()
	{
		ClearSelection();
		GC.SuppressFinalize(this);
	}

	private void Cleanup(UmbrellaFileUploadStatus status)
	{
		if (_cancellationTokenSource is not null)
		{
			_cancellationTokenSource.Cancel();
			_cancellationTokenSource.Dispose();
			_cancellationTokenSource = null;
		}

		Status = status;
	}

	private void ClearSelection()
	{
		Cleanup(UmbrellaFileUploadStatus.None);
		SelectedFile = null;
	}
}