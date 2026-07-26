using System.Globalization;
using Umbrella.DynamicImage.Abstractions;
using Xamarin.Forms;

namespace Umbrella.Xamarin.Converters;

// TODO: Could make this public and encode the parameters in the ConverterParameter property using a pipe?
internal sealed class UmbrellaDynamicImageConverter : UmbrellaResponsiveImageConverter
{
	protected IDynamicImageUtility DynamicImageUtility { get; }

	protected string DynamicImagePathPrefix { get; }
	protected int WidthRequest { get; }
	protected int HeightRequest { get; }
	protected DynamicResizeMode ResizeMode { get; }
	protected DynamicImageFormat ImageFormat { get; }
	protected string? StripPrefix { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaDynamicImageConverter"/> class.
	/// </summary>
	/// <param name="maxPixelDensity">The maximum pixel density.</param>
	/// <param name="innerConverter">The inner converter.</param>
	/// <param name="dynamicImagePathPrefix">The dynamic image path prefix.</param>
	/// <param name="widthRequest">The width request.</param>
	/// <param name="heightRequest">The height request.</param>
	/// <param name="resizeMode">The resize mode.</param>
	/// <param name="imageFormat">The image format.</param>
	/// <param name="stripPrefix">The strip prefix.</param>
	public UmbrellaDynamicImageConverter(
		int maxPixelDensity,
		IValueConverter? innerConverter,
		string dynamicImagePathPrefix,
		int widthRequest,
		int heightRequest,
		DynamicResizeMode resizeMode,
		DynamicImageFormat imageFormat,
		string? stripPrefix)
		: base(maxPixelDensity, innerConverter)
	{
		DynamicImagePathPrefix = dynamicImagePathPrefix;
		WidthRequest = widthRequest;
		HeightRequest = heightRequest;
		ResizeMode = resizeMode;
		ImageFormat = imageFormat;
		StripPrefix = stripPrefix;

		DynamicImageUtility = UmbrellaXamarinServices.GetService<IDynamicImageUtility>();
	}

	/// <inheritdoc />
	public override object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value is string imageUrl)
		{
			if (imageUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || imageUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
				return imageUrl;

			string strippedUrl = StripUrlPrefix(imageUrl);

			var options = new DynamicImageOptions(strippedUrl, WidthRequest, HeightRequest, ResizeMode, ImageFormat);

			string path = DynamicImageUtility.GenerateVirtualPath(DynamicImagePathPrefix, options).TrimStart('~').Replace("//", "/");

			return base.Convert(path, targetType, parameter, culture);
		}

		return null;
	}

	private string StripUrlPrefix(string url) => !string.IsNullOrEmpty(StripPrefix) ? url.Remove(0, StripPrefix!.Length) : url;
}