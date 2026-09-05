
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using CommunityToolkit.Diagnostics;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Umbrella.AspNetCore.Blazor.Components.Dialog.Abstractions;
using Umbrella.AspNetCore.Blazor.Components.DynamicImage;
using Umbrella.AspNetCore.Blazor.Components.FileUpload;
using Umbrella.AspNetCore.Blazor.Constants;
using Umbrella.AspNetCore.Blazor.Services.Abstractions;
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
	/// <summary>Gets or sets image metadata resolved together on the server.</summary>
	[Parameter]
	public DynamicImageDescriptor? Image { get; set; }

	/// <summary>Gets or sets the approval for separately supplied focal coordinates.</summary>
	[Parameter]
	public string? FocalPointApproval { get; set; }

	/// <inheritdoc />
	public override Task SetParametersAsync(ParameterView parameters)
	{
		DynamicImageParameterValidation.Validate(parameters);
		return base.SetParametersAsync(parameters);
	}

	private ElementReference CropPreviewElement { get; set; }
	private string? UpdatedFocalPointApproval { get; set; }
	private const double KeyboardFocalPointStep = 0.01;

	private readonly string _focalPointInstructionsId = $"u-file-image-preview-upload-focal-instructions-{Guid.NewGuid():N}";
	private bool _focalPointSelectorInitialized;
	private bool _parametersInitialized;
	private string? _lastUrlParameter;
	private string? _lastVersionTokenParameter;
	private string? _lastApprovalParameter;
	private double? _lastFocalPointXParameter;
	private double? _lastFocalPointYParameter;

	[Inject]
	private ILogger<UmbrellaFileImagePreviewUpload> Logger { get; [RequiresUnreferencedCode(TrimConstants.DI)] set; } = null!;

	[Inject]
	private IUmbrellaDialogService DialogUtility { get; [RequiresUnreferencedCode(TrimConstants.DI)] set; } = null!;

	[Inject]
	private IUmbrellaBlazorInteropService BlazorInteropUtility { get; set; } = null!;

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
	/// Gets or sets the normalised X coordinate of the focal point, between 0 and 1 starting from the left of the image.
	/// </summary>
	[Parameter]
	public double? FocalPointX { get; set; }

	/// <summary>
	/// Gets or sets the normalised Y coordinate of the focal point, between 0 and 1 starting from the top of the image.
	/// </summary>
	[Parameter]
	public double? FocalPointY { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the image should expose an interactive focal-point selector.
	/// </summary>
	[Parameter]
	public bool EnableFocalPointSelection { get; set; }

	/// <summary>
	/// Gets or sets the callback invoked after the user changes or clears the focal point.
	/// </summary>
	[Parameter]
	public EventCallback<UmbrellaFileImagePreviewUploadFocalPointChangedEventArgs> OnFocalPointChanged { get; set; }

	/// <summary>
	/// Gets or sets the instructions displayed with the interactive focal-point selector.
	/// </summary>
	[Parameter]
	public string FocalPointSelectionText { get; set; } = "Click or tap the image to set the focal point. Use the arrow keys for fine adjustment.";

	/// <summary>
	/// Gets or sets the clear focal point button text.
	/// </summary>
	[Parameter]
	public string ClearFocalPointButtonText { get; set; } = "Clear focal point";

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
	/// Gets or sets the version token associated with <see cref="Url"/>.
	/// </summary>
	[Parameter]
	public string? VersionToken { get; set; }

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
	private string? UpdatedImageVersionToken { get; set; }
	internal double? UpdatedFocalPointX { get; private set; }
	internal double? UpdatedFocalPointY { get; private set; }
	private UmbrellaFileImagePreviewUploadMode FileUploadMode { get; set; }
	private ElementReference FocalPointSelectorElement { get; set; }
	private string FocalPointInstructionsId => _focalPointInstructionsId;
	private string? FocalPointMarkerStyle => UpdatedFocalPointX.HasValue
		? FormattableString.Invariant($"left: {UpdatedFocalPointX.Value * 100:G4}%; top: {UpdatedFocalPointY!.Value * 100:G4}%")
		: null;
	private string FocalPointAriaValueText => UpdatedFocalPointX.HasValue
		? string.Create(CultureInfo.CurrentCulture, $"Horizontal {UpdatedFocalPointX.Value:P0}, vertical {UpdatedFocalPointY!.Value:P0}")
		: "No focal point selected";

	/// <inheritdoc />
	protected override void OnParametersSet()
	{
		string? url = Image is not null ? Image.Url : Url;
		string? version = Image is not null ? Image.VersionToken : VersionToken;
		double? x = Image is not null ? Image.FocalPoint?.X : FocalPointX;
		double? y = Image is not null ? Image.FocalPoint?.Y : FocalPointY;
		string? approval = Image is not null ? Image.FocalPointApproval : FocalPointApproval;
		ValidateFocalPoint(x, y);

		if (EnableFocalPointSelection && ResizeMode is not DynamicResizeMode.CropFocalPoint)
			throw new InvalidOperationException($"{nameof(EnableFocalPointSelection)} can only be used with {nameof(DynamicResizeMode.CropFocalPoint)}.");

		bool imageParametersChanged = !_parametersInitialized ||
			!string.Equals(url, _lastUrlParameter, StringComparison.OrdinalIgnoreCase) ||
			!string.Equals(version, _lastVersionTokenParameter, StringComparison.Ordinal);

		if (imageParametersChanged)
		{
			SetImage(url, version, isParameterUpdate: true);
		}

		if (imageParametersChanged || x != _lastFocalPointXParameter || y != _lastFocalPointYParameter)
		{
			SetFocalPoint(
				UpdatedImageUrl is null ? null : x,
				UpdatedImageUrl is null ? null : y);
		}

		if (imageParametersChanged || x != _lastFocalPointXParameter || y != _lastFocalPointYParameter || approval != _lastApprovalParameter)
			UpdatedFocalPointApproval = approval;

		ValidateFocalPoint(UpdatedFocalPointX, UpdatedFocalPointY);

		if (!EnableFocalPointSelection || UpdatedImageUrl is null)
			_focalPointSelectorInitialized = false;

		_parametersInitialized = true;
		_lastUrlParameter = url;
		_lastVersionTokenParameter = version;
		_lastApprovalParameter = approval;
		_lastFocalPointXParameter = x;
		_lastFocalPointYParameter = y;
	}

	/// <inheritdoc />
	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
		if (EnableFocalPointSelection && UpdatedImageUrl is not null && !_focalPointSelectorInitialized)
		{
			await BlazorInteropUtility.InitializeImageFocalPointSelectorAsync(FocalPointSelectorElement);
			_focalPointSelectorInitialized = true;
		}

		if (EnableFocalPointSelection && UpdatedImageUrl is not null)
			await BlazorInteropUtility.UpdateImageFocalPointPreviewAsync(FocalPointSelectorElement, CropPreviewElement, WidthRequest, HeightRequest, UpdatedFocalPointX, UpdatedFocalPointY);
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
			UpdatedImageVersionToken = null;
			_focalPointSelectorInitialized = false;
			SetFocalPoint(null, null);
			FileUploadMode = UmbrellaFileImagePreviewUploadMode.Upload;

			await NotifyFocalPointChangedAsync();

			if (OnDeleteImage.HasDelegate)
				await OnDeleteImage.InvokeAsync(EventArgs.Empty);
		}
		catch (Exception exc) when (Logger.WriteError(exc))
		{
			await DialogUtility.ShowDangerMessageAsync();
		}
	}

	/// <summary>
	/// Updates the thumbnail <see cref="Url"/>, its optional version token, and its optional focal point. This should be called manually by the component consumer after uploading a new image.
	/// </summary>
	/// <param name="url">The new thumbnail URL.</param>
	/// <param name="versionToken">The optional version token associated with <paramref name="url"/>.</param>
	/// <param name="focalPointX">The optional normalised X coordinate of the focal point.</param>
	/// <param name="focalPointY">The optional normalised Y coordinate of the focal point.</param>
	/// <param name="focalPointApproval">The optional server-issued focal approval.</param>
	public void Update(string? url, string? versionToken = null, double? focalPointX = null, double? focalPointY = null, string? focalPointApproval = null)
	{
		ValidateFocalPoint(focalPointX, focalPointY);
		SetImage(url, versionToken, isParameterUpdate: false);
		SetFocalPoint(string.IsNullOrWhiteSpace(url) ? null : focalPointX, string.IsNullOrWhiteSpace(url) ? null : focalPointY);
		UpdatedFocalPointApproval = focalPointApproval;
	}

	/// <summary>Atomically replaces the preview metadata after an upload or save.</summary>
	/// <param name="image">The new descriptor, or null to clear the image.</param>
	public void UpdateImage(DynamicImageDescriptor? image)
		=> Update(image?.Url, image?.VersionToken, image?.FocalPoint?.X, image?.FocalPoint?.Y, image?.FocalPointApproval);

	private async Task FocalPointClickAsync(MouseEventArgs args)
	{
		try
		{
			UmbrellaImageBounds bounds = await BlazorInteropUtility.GetImageBoundsAsync(FocalPointSelectorElement);
			(double x, double y) = NormalizeFocalPoint(args.ClientX, args.ClientY, bounds.Left, bounds.Top, bounds.Width, bounds.Height);
			await SetFocalPointAndNotifyAsync(x, y);
		}
		catch (Exception exc) when (Logger.WriteError(exc))
		{
			await DialogUtility.ShowDangerMessageAsync();
		}
	}

	internal async Task FocalPointKeyDownAsync(KeyboardEventArgs args)
	{
		double x = UpdatedFocalPointX ?? 0.5;
		double y = UpdatedFocalPointY ?? 0.5;
		double step = args.ShiftKey ? KeyboardFocalPointStep * 10 : KeyboardFocalPointStep;

		switch (args.Key)
		{
			case "ArrowLeft":
				x -= step;
				break;
			case "ArrowRight":
				x += step;
				break;
			case "ArrowUp":
				y -= step;
				break;
			case "ArrowDown":
				y += step;
				break;
			default:
				return;
		}

		await SetFocalPointAndNotifyAsync(Math.Clamp(x, 0, 1), Math.Clamp(y, 0, 1));
	}

	internal async Task ClearFocalPointAsync()
	{
		SetFocalPoint(null, null);
		await NotifyFocalPointChangedAsync();
	}

	internal async Task SetFocalPointAndNotifyAsync(double focalPointX, double focalPointY)
	{
		ValidateFocalPoint(focalPointX, focalPointY);
		SetFocalPoint(focalPointX, focalPointY);
		await NotifyFocalPointChangedAsync();
	}

	private async Task NotifyFocalPointChangedAsync()
	{
		if (OnFocalPointChanged.HasDelegate)
		{
			await OnFocalPointChanged.InvokeAsync(new UmbrellaFileImagePreviewUploadFocalPointChangedEventArgs(
				UpdatedFocalPointX,
				UpdatedFocalPointY));
		}
	}

	private void SetImage(string? url, string? versionToken, bool isParameterUpdate)
	{
		string? currentImageUrl = UpdatedImageUrl;
		UpdatedImageUrl = string.IsNullOrWhiteSpace(url) ? null : url;
		UpdatedImageVersionToken = UpdatedImageUrl is null ? null : versionToken;

		if (UpdatedImageUrl is null)
			_focalPointSelectorInitialized = false;

		FileUploadMode = UpdatedImageUrl is null
			? UmbrellaFileImagePreviewUploadMode.Upload
			: isParameterUpdate || currentImageUrl is null || currentImageUrl.Equals(UpdatedImageUrl, StringComparison.OrdinalIgnoreCase)
				? UmbrellaFileImagePreviewUploadMode.Current
				: UmbrellaFileImagePreviewUploadMode.New;
	}

	private void SetFocalPoint(double? focalPointX, double? focalPointY)
	{
		if (UpdatedFocalPointX != focalPointX || UpdatedFocalPointY != focalPointY)
			UpdatedFocalPointApproval = null;

		UpdatedFocalPointX = focalPointX;
		UpdatedFocalPointY = focalPointY;
	}

	private void ValidateFocalPoint(double? focalPointX, double? focalPointY)
	{
		if (focalPointX.HasValue != focalPointY.HasValue)
			throw new ArgumentException($"Both {nameof(FocalPointX)} and {nameof(FocalPointY)} must be defined if either is specified.");

		if (!focalPointX.HasValue)
			return;

		Guard.IsBetweenOrEqualTo(focalPointX.Value, 0, 1);
		Guard.IsBetweenOrEqualTo(focalPointY!.Value, 0, 1);

		if (ResizeMode is not DynamicResizeMode.CropFocalPoint)
			throw new InvalidOperationException($"{nameof(FocalPointX)} and {nameof(FocalPointY)} can only be used with {nameof(DynamicResizeMode.CropFocalPoint)}.");
	}

	internal static (double X, double Y) NormalizeFocalPoint(
		double clientX,
		double clientY,
		double imageLeft,
		double imageTop,
		double imageWidth,
		double imageHeight)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(imageWidth);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(imageHeight);

		return (
			Math.Clamp((clientX - imageLeft) / imageWidth, 0, 1),
			Math.Clamp((clientY - imageTop) / imageHeight, 0, 1));
	}
}
