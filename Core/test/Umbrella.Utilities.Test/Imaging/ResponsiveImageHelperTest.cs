using Microsoft.Extensions.Logging;
using Moq;
using Umbrella.Utilities.Imaging;

namespace Umbrella.Utilities.Test.Imaging;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1861:Avoid constant arrays as arguments", Justification = "Unit Tests")]
public class ResponsiveImageHelperTest
{
	[Theory]
	[InlineData(null, new int[0])]
	[InlineData("", new int[0])]
	[InlineData("    ", new int[0])]
	[InlineData("1,2", new[] { 1, 2 })]
	[InlineData("1,2,3", new[] { 1, 2, 3 })]
	[InlineData("1,,,2,,,3", new[] { 1, 2, 3 })]
	[InlineData("1,1,,2,2,,3", new[] { 1, 2, 3 })]
	[InlineData("1,1,pogba,2,2,,3", new[] { 1, 2, 3 })]
	public void GetParsedIntegerItems_Valid(string input, int[] output)
	{
		var helper = CreateResponsiveImageHelper();

		var result = helper.GetParsedIntegerItems(input);

		Assert.Equal(output, result);
	}

	[Theory]
	[InlineData(1, new[] { 1 })]
	[InlineData(2, new[] { 1, 2 })]
	[InlineData(3, new[] { 1, 2, 3 })]

	public void GetPixelDensities_Valid(int maxDensity, int[] output)
	{
		var helper = CreateResponsiveImageHelper();

		var result = helper.GetPixelDensities(maxDensity);

		Assert.Equal(output, result);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void GetPixelDensities_Invalid(int maxDensity)
	{
		var helper = CreateResponsiveImageHelper();

		_ = Assert.Throws<ArgumentOutOfRangeException>(() => helper.GetPixelDensities(maxDensity));
	}

	[Theory]
	[InlineData("/images/test.png", 1, "")]
	[InlineData("/images/test.png", 2, "/images/test.png 1x, /images/test@2x.png 2x")]
	[InlineData("/images/test.png", 3, "/images/test.png 1x, /images/test@2x.png 2x, /images/test@3x.png 3x")]
	[InlineData("/images/test.png?key=X.Y.Z&o=k", 2, "/images/test.png?key=X.Y.Z&o=k 1x, /images/test@2x.png?key=X.Y.Z&o=k 2x")]
	[InlineData("/images/test.png?key=X.Y.Z&o=k", 3, "/images/test.png?key=X.Y.Z&o=k 1x, /images/test@2x.png?key=X.Y.Z&o=k 2x, /images/test@3x.png?key=X.Y.Z&o=k 3x")]
	public void GetPixelDensitySrcSetValue_Valid(string imageUrl, int maxPixelDensity, string output)
	{
		var helper = CreateResponsiveImageHelper();

		string result = helper.GetPixelDensitySrcSetValue(imageUrl, maxPixelDensity);

		Assert.Equal(output, result);
	}

	[Theory]
	[InlineData("/images/test.png", 1, new[] { "/images/test.png" })]
	[InlineData("/images/test.png", 2, new[] { "/images/test.png", "/images/test@2x.png" })]
	[InlineData("/images/test.png", 3, new[] { "/images/test.png", "/images/test@2x.png", "/images/test@3x.png" })]
	[InlineData("/images/test.png?key=X.Y.Z&o=k", 2, new[] { "/images/test.png?key=X.Y.Z&o=k", "/images/test@2x.png?key=X.Y.Z&o=k" })]
	[InlineData("/images/test.png?key=X.Y.Z&o=k", 3, new[] { "/images/test.png?key=X.Y.Z&o=k", "/images/test@2x.png?key=X.Y.Z&o=k", "/images/test@3x.png?key=X.Y.Z&o=k" })]
	public void GetPixelDensityImageUrls_Valid(string imageUrl, int maxPixelDensity, string[] output)
	{
		var helper = CreateResponsiveImageHelper();

		var result = helper.GetPixelDensityImageUrls(imageUrl, maxPixelDensity);

		Assert.Equal(output, result);
	}

	[Theory]
	[InlineData("/images/test.png", 1, "/images/test.png")]
	[InlineData("/images/test.png", 2, "/images/test@2x.png")]
	[InlineData("/images/test.png", 3, "/images/test@3x.png")]
	[InlineData("/images/test.png?key=X.Y.Z&o=k", 1, "/images/test.png?key=X.Y.Z&o=k")]
	[InlineData("/images/test.png?key=X.Y.Z&o=k", 2, "/images/test@2x.png?key=X.Y.Z&o=k")]
	[InlineData("/images/test.png?key=X.Y.Z&o=k", 3, "/images/test@3x.png?key=X.Y.Z&o=k")]
	public void GetPixelDensityImageUrl_Valid(string imageUrl, int pixelDensity, string output)
	{
		var helper = CreateResponsiveImageHelper();

		string result = helper.GetPixelDensityImageUrl(imageUrl, pixelDensity);

		Assert.Equal(output, result);
	}

	[Theory]
	[InlineData("/images/test.png", "100", 1, 200, 100, "/100/50/images/test.png 100w")]
	[InlineData("/images/test.png", "100, 200", 1, 200, 100, "/100/50/images/test.png 100w, /200/100/images/test.png 200w")]
	[InlineData("/images/test.png", "100, 200, 300", 1, 200, 100, "/100/50/images/test.png 100w, /200/100/images/test.png 200w, /300/150/images/test.png 300w")]
	[InlineData("/images/test.png", "100", 2, 200, 100, "/100/50/images/test.png 100w, /200/100/images/test.png 200w")]
	[InlineData("/images/test.png", "100, 200", 2, 200, 100, "/100/50/images/test.png 100w, /200/100/images/test.png 200w, /400/200/images/test.png 400w")]
	[InlineData("/images/test.png", "100, 200, 300", 2, 200, 100, "/100/50/images/test.png 100w, /200/100/images/test.png 200w, /300/150/images/test.png 300w, /400/200/images/test.png 400w, /600/300/images/test.png 600w")]
	[InlineData("/images/test.png", "100", 3, 200, 100, "/100/50/images/test.png 100w, /200/100/images/test.png 200w, /300/150/images/test.png 300w")]
	[InlineData("/images/test.png", "100, 200", 3, 200, 100, "/100/50/images/test.png 100w, /200/100/images/test.png 200w, /300/150/images/test.png 300w, /400/200/images/test.png 400w, /600/300/images/test.png 600w")]
	[InlineData("/images/test.png", "100, 200, 300", 3, 200, 100, "/100/50/images/test.png 100w, /200/100/images/test.png 200w, /300/150/images/test.png 300w, /400/200/images/test.png 400w, /600/300/images/test.png 600w, /900/450/images/test.png 900w")]
	[InlineData("/images/test.png?key=X.Y.Z&o=k", "100", 1, 200, 100, "/100/50/images/test.png?key=X.Y.Z&o=k 100w")]
	[InlineData("/images/test.png?key=X.Y.Z&o=k", "100, 200", 1, 200, 100, "/100/50/images/test.png?key=X.Y.Z&o=k 100w, /200/100/images/test.png?key=X.Y.Z&o=k 200w")]
	[InlineData("/images/test.png?key=X.Y.Z&o=k", "100, 200, 300", 1, 200, 100, "/100/50/images/test.png?key=X.Y.Z&o=k 100w, /200/100/images/test.png?key=X.Y.Z&o=k 200w, /300/150/images/test.png?key=X.Y.Z&o=k 300w")]
	[InlineData("/images/test.png?key=X.Y.Z&o=k", "100", 2, 200, 100, "/100/50/images/test.png?key=X.Y.Z&o=k 100w, /200/100/images/test.png?key=X.Y.Z&o=k 200w")]
	[InlineData("/images/test.png?key=X.Y.Z&o=k", "100, 200", 2, 200, 100, "/100/50/images/test.png?key=X.Y.Z&o=k 100w, /200/100/images/test.png?key=X.Y.Z&o=k 200w, /400/200/images/test.png?key=X.Y.Z&o=k 400w")]
	[InlineData("/images/test.png?key=X.Y.Z&o=k", "100, 200, 300", 2, 200, 100, "/100/50/images/test.png?key=X.Y.Z&o=k 100w, /200/100/images/test.png?key=X.Y.Z&o=k 200w, /300/150/images/test.png?key=X.Y.Z&o=k 300w, /400/200/images/test.png?key=X.Y.Z&o=k 400w, /600/300/images/test.png?key=X.Y.Z&o=k 600w")]
	[InlineData("/images/test.png?key=X.Y.Z&o=k", "100", 3, 200, 100, "/100/50/images/test.png?key=X.Y.Z&o=k 100w, /200/100/images/test.png?key=X.Y.Z&o=k 200w, /300/150/images/test.png?key=X.Y.Z&o=k 300w")]
	[InlineData("/images/test.png?key=X.Y.Z&o=k", "100, 200", 3, 200, 100, "/100/50/images/test.png?key=X.Y.Z&o=k 100w, /200/100/images/test.png?key=X.Y.Z&o=k 200w, /300/150/images/test.png?key=X.Y.Z&o=k 300w, /400/200/images/test.png?key=X.Y.Z&o=k 400w, /600/300/images/test.png?key=X.Y.Z&o=k 600w")]
	[InlineData("/images/test.png?key=X.Y.Z&o=k", "100, 200, 300", 3, 200, 100, "/100/50/images/test.png?key=X.Y.Z&o=k 100w, /200/100/images/test.png?key=X.Y.Z&o=k 200w, /300/150/images/test.png?key=X.Y.Z&o=k 300w, /400/200/images/test.png?key=X.Y.Z&o=k 400w, /600/300/images/test.png?key=X.Y.Z&o=k 600w, /900/450/images/test.png?key=X.Y.Z&o=k 900w")]
	public void GetSizeSrcSetValue_Valid(string imageUrl, string sizeWidths, int maxPixelDensity, int widthRequest, int heightRequest, string output)
	{
		var helper = CreateResponsiveImageHelper();

		string result = helper.GetSizeSrcSetValue(imageUrl, sizeWidths, maxPixelDensity, widthRequest, heightRequest, opts => $"/{opts.imageWidth}/{opts.imageHeight}{opts.path}");

		Assert.Equal(output, result);
	}

	private static ResponsiveImageHelper CreateResponsiveImageHelper()
	{
		var logger = new Mock<ILogger<ResponsiveImageHelper>>();

		return new ResponsiveImageHelper(logger.Object);
	}
}