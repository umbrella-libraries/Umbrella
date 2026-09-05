using System.Text.Json;
using Umbrella.DynamicImage.Abstractions;
using Umbrella.FileSystem.Abstractions;
using Umbrella.Internal.Mocks;

namespace Umbrella.AspNetCore.WebUtilities.DynamicImage.Test;

public class DynamicImageFocalPointApprovalTest
{
	internal static void AssertRenderedApprovals(string html, DynamicImageFocalPointApprovalService service)
	{
		var utility = new DynamicImageUtility(CoreUtilitiesMocks.CreateLogger<DynamicImageUtility>());
		string[] urls = [.. WebUtility.HtmlDecode(html).Split('"').SelectMany(part => part.Split(' ')).Where(part => part.StartsWith("/dynamicimage/", StringComparison.Ordinal)).Distinct()];
		Assert.NotEmpty(urls);
		foreach (string url in urls)
		{
			var parsed = utility.TryParseUrl("dynamicimage", url);
			Assert.Equal(DynamicImageParseUrlResult.Success, parsed.status);
			Assert.Equal("/images/test.jpg", parsed.imageOptions.SourcePath);
			Assert.True(service.Verify(parsed.imageOptions), url);
		}
	}

	[Theory]
	[InlineData("/media", "/media/images/test.jpg", "/images/test.jpg")]
	[InlineData(null, "/files/images/test.jpg", "/files/images/test.jpg")]
	[InlineData("", "/files/images/test.jpg", "/files/images/test.jpg")]
	public void SigningPrefixCanBeCustomizedOrDisabled(string? prefix, string url, string expectedSource)
	{
		var service = new DynamicImageFocalPointApprovalService(
			new() { ActiveKeyId = "key", Keys = new Dictionary<string, string> { ["key"] = Convert.ToBase64String(new byte[32]) }, StripPrefix = prefix },
			new(), CoreUtilitiesMocks.CreateLogger<DynamicImageFocalPointApprovalService>());
		var image = service.Create(new UmbrellaVersionedUrl(url, "version"), 0.25, 0.75)!;
		Assert.True(service.Verify(ToOptions(image, path: expectedSource)));
	}

	internal static DynamicImageFocalPointApprovalService CreateService(string active = "keyA", bool includeOld = true, string route = "dynamicimage")
	{
		var keys = new Dictionary<string, string> { [active] = Convert.ToBase64String(Enumerable.Repeat((byte)(active is "keyA" ? 1 : 2), 32).ToArray()) };
		if (includeOld)
			keys["keyA"] = Convert.ToBase64String(Enumerable.Repeat((byte)1, 32).ToArray());
		return new(new() { ActiveKeyId = active, Keys = keys }, new() { DynamicImagePathPrefix = route }, CoreUtilitiesMocks.CreateLogger<DynamicImageFocalPointApprovalService>());
	}

	internal static DynamicImageOptions ToOptions(DynamicImageDescriptor image, string? path = null, string? version = null, double? x = null, string? approval = null)
		=> new(path ?? image.Url, 100, 50, DynamicResizeMode.CropFocalPoint, DynamicImageFormat.WebP, focalPointX: x ?? image.FocalPoint!.Value.X, focalPointY: image.FocalPoint!.Value.Y, versionToken: version ?? image.VersionToken, focalPointApproval: approval ?? image.FocalPointApproval);

	[Fact]
	public void ApprovalIsStableAndRoundTripsThroughJsonAndUrls()
	{
		var service = CreateService();
		var descriptor = service.Create(new UmbrellaVersionedUrl("/images/test.jpg", "version"), 0.123456, 0.75)!;
		Assert.Equal(descriptor, CreateService().Create(new UmbrellaVersionedUrl("/images/test.jpg", "version"), 0.123456, 0.75));
		var deserialized = JsonSerializer.Deserialize<DynamicImageDescriptor>(JsonSerializer.Serialize(descriptor));
		Assert.Equal(descriptor, deserialized);
		var utility = new DynamicImageUtility(CoreUtilitiesMocks.CreateLogger<DynamicImageUtility>());
		string url = utility.GenerateVirtualPath("dynamicimage", ToOptions(deserialized!)).TrimStart('~');
		var parsed = utility.TryParseUrl("dynamicimage", url);
		Assert.Equal(DynamicImageParseUrlResult.Success, parsed.status);
		Assert.True(service.Verify(parsed.imageOptions));
		Assert.Equal(0.1235, parsed.imageOptions.FocalPointX);
	}

	[Fact]
	public void ImageVersionCoordinatesAndRouteAreBound()
	{
		var service = CreateService();
		var image = service.Create(new UmbrellaVersionedUrl("/images/test.jpg", "version"), 0.25, 0.75)!;
		Assert.False(service.Verify(ToOptions(image, path: "/images/other.jpg")));
		Assert.False(service.Verify(ToOptions(image, version: "replacement")));
		Assert.False(service.Verify(ToOptions(image, x: 0.5)));
		Assert.False(CreateService(route: "other").Verify(ToOptions(image)));
		Assert.False(service.Verify(ToOptions(image, approval: "1.keyA.invalid")));
		Assert.True(service.Verify(new DynamicImageOptions(image.Url, 800, 600, DynamicResizeMode.CropFocalPoint, DynamicImageFormat.Avif, focalPointX: 0.25, focalPointY: 0.75, versionToken: image.VersionToken, focalPointApproval: image.FocalPointApproval)));
	}

	[Fact]
	public void RotationRetainsOldApprovalsOnlyWhileOldKeyIsRetained()
	{
		var image = CreateService().Create(new UmbrellaVersionedUrl("/images/test.jpg", "version"), 0.25, 0.75)!;
		Assert.True(CreateService("keyB").Verify(ToOptions(image)));
		Assert.False(CreateService("keyB", includeOld: false).Verify(ToOptions(image)));
		Assert.NotEqual(image.FocalPointApproval, CreateService("keyB").Create(new UmbrellaVersionedUrl(image.Url, image.VersionToken), 0.25, 0.75)!.FocalPointApproval);
	}

	[Fact]
	public void OrdinaryImagesAndMissingFilesDoNotRequireKeys()
	{
		var service = new DynamicImageFocalPointApprovalService(new(), new(), CoreUtilitiesMocks.CreateLogger<DynamicImageFocalPointApprovalService>());
		Assert.Null(service.Create(null, 0.25, 0.75));
		Assert.Null(service.Create(new UmbrellaVersionedUrl("/images/test.jpg", null))!.FocalPointApproval);
		_ = Assert.Throws<InvalidOperationException>(() => service.Create(new UmbrellaVersionedUrl("/images/test.jpg", "version"), 0.25, 0.75));
		_ = Assert.Throws<ArgumentException>(() => CreateService().Create(new UmbrellaVersionedUrl("/images/test.jpg", null), 0.25, 0.75));
	}

	[Theory]
	[InlineData("/images/../test.jpg")]
	[InlineData("/images/%2e%2e/test.jpg")]
	[InlineData("/images//test.jpg")]
	[InlineData("https://example.com/test.jpg")]
	[InlineData("/images/test.jpg?fpx=0.5")]
	public void AmbiguousOrExternalPathsCannotBeApproved(string path)
		=> Assert.Throws<ArgumentException>(() => CreateService().Create(new UmbrellaVersionedUrl(path, "version"), 0.25, 0.75));

	[Theory]
	[InlineData("fpx=0.2&fpy=0.3&fpx=0.4")]
	[InlineData("fpx=0.2&fpy=0.3&%66px=0.4")]
	[InlineData("fpx=NaN&fpy=0.3")]
	[InlineData("fpx=Infinity&fpy=0.3")]
	[InlineData("fpx=&fpy=0.3")]
	[InlineData("fpx&fpy=0.3")]
	[InlineData("fpa=invalid")]
	[InlineData("fpx=0.2&fpy=0.3&fpa=a&fpa=b")]
	public void InvalidQueryParametersAreRejected(string query)
	{
		var utility = new DynamicImageUtility(CoreUtilitiesMocks.CreateLogger<DynamicImageUtility>());
		Assert.Equal(DynamicImageParseUrlResult.Invalid, utility.TryParseUrl("dynamicimage", "/dynamicimage/100/50/CropFocalPoint/jpg/images/test.webp?" + query).status);
	}
}
