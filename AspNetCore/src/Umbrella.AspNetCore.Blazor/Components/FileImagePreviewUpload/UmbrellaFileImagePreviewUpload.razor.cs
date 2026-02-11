
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Umbrella.AspNetCore.Blazor.Components.Dialog.Abstractions;
using Umbrella.AspNetCore.Blazor.Components.FileUpload;
using Umbrella.AspNetCore.Blazor.Constants;
using Umbrella.DynamicImage.Abstractions;
using Umbrella.Utilities.Imaging;
using Umbrella.Utilities.Primitives.Abstractions;

namespace Umbrella.AspNetCore.Blazor.Components.FileImagePreviewUpload;

/// <summary>
/// A component that can be used to upload image files that wraps the <see cref="UmbrellaFileUpload"/> component with support for
/// displaying a preview of the upload image.
/// </summary>
/// <seealso cref="ComponentBase" />
public partial class UmbrellaFileImagePreviewUpload : ComponentBase
{
	[Inject]
	private ILogger<UmbrellaFileImagePreviewUpload> Logger { get; [RequiresUnreferencedCode(TrimConstants.DI)] set; } = null!;

	[Inject]
	private IUmbrellaDialogService DialogUtility { get; [RequiresUnreferencedCode(TrimConstants.DI)] set; } = null!;

	/// <summary>
	/// Gets or sets the message shown when a new image has been uploaded in place of an existing one.
	/// </summary>
	[Parameter]
	public string ChangesMadeMessage { get; set; } = "You have made changes to this image. These changes will be saved when this page is saved.";

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
	public Func<UmbrellaFileUploadRequestEventArgs, Task<IOperationResult>>? OnRequestUpload { get; set; }

	/// <summary>
	/// Gets or sets the target width of the resized image. The resized image width may be less than this value depending on the width of the uploaded source image.
	/// </summary>
	/// <remarks>Defaults to 1</remarks>
	[Parameter]
	public int WidthRequest { get; set; } = 1;

	/// <summary>
	/// Gets or sets the target height of the resized image. The resized image height may be less than this value depending on the height of the uploaded source image.
	/// </summary>
	/// <remarks>Defaults to 1</remarks>
	[Parameter]
	public int HeightRequest { get; set; } = 1;

	/// <summary>
	/// Gets or sets the mode to use when resizing images.
	/// </summary>
	/// <remarks>
	/// Defaults to <see cref="DynamicResizeMode.Crop"/>
	/// </remarks>
	[Parameter]
	public DynamicResizeMode ResizeMode { get; set; } = DynamicResizeMode.Crop;

	/// <summary>
	/// Gets or sets the image format of the resized image.
	/// </summary>
	/// <remarks>Defaults to <see cref="DynamicImageFormat.Jpeg"/></remarks>
	[Parameter]
	public DynamicImageFormat ImageFormat { get; set; } = DynamicImageFormat.Jpeg;

	/// <summary>
	/// Gets or sets the size widths.
	/// </summary>
	/// <remarks>
	/// <para>
	/// If specified, these are used in combination with the values of <see cref="MaxPixelDensity"/>,
	/// <see cref="WidthRequest"/> and <see cref="HeightRequest"/> to set the value of the srcset attribute on the rendered img tag.
	/// </para>
	/// <para>
	/// Please see the unit tests for <see cref="ResponsiveImageHelper.GetSizeSrcSetValue"/> for sample data.
	/// </para>
	/// </remarks>
	[Parameter]
	public string? SizeWidths { get; set; }

	/// <summary>
	/// Gets or sets the maximum pixel density image that should be rendered for the preview thumbnail.
	/// </summary>
	/// <remarks>
	/// Defaults to 4.
	/// </remarks>
	[Parameter]
	public int MaxPixelDensity { get; set; } = 4;

	/// <summary>
	/// Gets or sets the URL.
	/// </summary>
	[Parameter]
	public string? Url { get; set; }

	/// <summary>
	/// Gets or sets the delegate that is invoked when the Delete button is clicked when there is an existing image.
	/// </summary>
	[Parameter]
	public EventCallback OnDeleteImage { get; set; }

	/// <summary>
	/// Gets or sets the delete button text.
	/// </summary>
	/// <remarks>Defaults to <c>Delete</c></remarks>
	[Parameter]
	public string DeleteButtonText { get; set; } = "Delete";

	private string? UpdatedImageUrl { get; set; }
	private UmbrellaFileImagePreviewUploadMode FileUploadMode { get; set; }

	/// <inheritdoc />
	protected override void OnInitialized()
	{
		if (!string.IsNullOrWhiteSpace(Url))
		{
			UpdatedImageUrl = Url;
			FileUploadMode = UmbrellaFileImagePreviewUploadMode.Current;
		}
	}

	private async Task<IOperationResult?> OnRequestUploadInnerAsync(UmbrellaFileUploadRequestEventArgs args)
	{
		try
		{
			if (OnRequestUpload is null)
				throw new InvalidOperationException($"The {nameof(OnRequestUpload)} property does not have an assigned delegate.");

			IOperationResult result = await OnRequestUpload(args);

			StateHasChanged();

			return result;
		}
		catch (Exception exc) when (Logger.WriteError(exc))
		{
			await DialogUtility.ShowDangerMessageAsync();

			return null;
		}
	}

	private async Task DeleteImageClickAsync()
	{
		try
		{
			bool delete = await DialogUtility.ShowConfirmDangerMessageAsync("Are you sure you want to delete this image? This change will not take effect until this page is saved.", "Delete Image");

			if (!delete)
				return;

			UpdatedImageUrl = null;
			FileUploadMode = UmbrellaFileImagePreviewUploadMode.Upload;

			if (OnDeleteImage.HasDelegate)
				await OnDeleteImage.InvokeAsync(EventArgs.Empty);
		}
		catch (Exception exc) when (Logger.WriteError(exc))
		{
			await DialogUtility.ShowDangerMessageAsync();
		}
	}

	/// <summary>
	/// Updates the thumbnail <see cref="Url"/>. This should be called manually by the component consumer after uploading a new image.
	/// </summary>
	/// <param name="url">The new thumbnail URL.</param>
	public void Update(string? url)
	{
		if (string.IsNullOrWhiteSpace(url))
		{
			UpdatedImageUrl = null;
			FileUploadMode = UmbrellaFileImagePreviewUploadMode.Upload;
		}
		else
		{
			string? currentImageUrl = UpdatedImageUrl;
			UpdatedImageUrl = url;
			FileUploadMode = currentImageUrl?.Equals(UpdatedImageUrl, StringComparison.OrdinalIgnoreCase) is true ? UmbrellaFileImagePreviewUploadMode.Current : UmbrellaFileImagePreviewUploadMode.New;
		}
	}
}