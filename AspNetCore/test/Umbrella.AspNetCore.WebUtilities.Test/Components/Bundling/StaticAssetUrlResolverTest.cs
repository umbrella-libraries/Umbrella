using Microsoft.AspNetCore.Components;
using Umbrella.AspNetCore.WebUtilities.Components.Bundling;
using Umbrella.AspNetCore.WebUtilities.Components.Options;

namespace Umbrella.AspNetCore.WebUtilities.Test.Components.Bundling;

public class StaticAssetUrlResolverTest
{
	public static TheoryData<string, string, string?> NormalizeAssetKeyData { get; } = new()
	{
		{ "dist", "/dist/site.js", "dist/site.js" },
		{ "dist", "~/dist/site.js", "dist/site.js" },
		{ "dist", "/dist/site.js?v=123", "dist/site.js" },
		{ "dist", "/dist/site.js#hash", "dist/site.js" },
		{ "dist", "/my-app/dist/site.js", "dist/site.js" },
		{ "dist", @"\dist\site.js", "dist/site.js" },
		{ "assets", "/my-app/assets/site.js", "assets/site.js" },
		{ "dist", "https://cdn.example.com/dist/site.js", null },
		{ "dist", "", null }
	};

	[Theory]
	[MemberData(nameof(NormalizeAssetKeyData))]
	public void NormalizeAssetKey_ReturnsExpectedValue(string staticAssetPathPrefix, string path, string? expectedResult)
	{
		BundleComponentOptions options = CreateOptions(staticAssetPathPrefix);

		string? result = StaticAssetUrlResolver.NormalizeAssetKey(path, options.StaticAssetPathPrefixes);

		Assert.Equal(expectedResult, result);
	}

	[Fact]
	public void Resolve_ReturnsFingerprintUrl_WhenAssetExists()
	{
		var assets = new ResourceAssetCollection(
		[
			new ResourceAsset("dist/site.8ncclf0mr4.js", [new ResourceAssetProperty("label", "dist/site.js")])
		]);
		BundleComponentOptions options = CreateOptions("dist");

		string result = StaticAssetUrlResolver.Resolve(assets, "/dist/site.js", options);

		Assert.Equal("dist/site.8ncclf0mr4.js", result);
	}

	[Fact]
	public void Resolve_ReturnsOriginalPath_WhenAssetDoesNotExist()
	{
		var assets = new ResourceAssetCollection(
		[
			new ResourceAsset("dist/vendor.js", [new ResourceAssetProperty("label", "dist/vendor.js")])
		]);
		BundleComponentOptions options = CreateOptions("dist");

		string result = StaticAssetUrlResolver.Resolve(assets, "/dist/site.js?v=123", options);

		Assert.Equal("/dist/site.js?v=123", result);
	}

	[Fact]
	public void Resolve_ReturnsFingerprintUrl_WhenCustomAssetPathPrefixExists()
	{
		var assets = new ResourceAssetCollection(
		[
			new ResourceAsset("assets/site.8ncclf0mr4.js", [new ResourceAssetProperty("label", "assets/site.js")])
		]);
		BundleComponentOptions options = CreateOptions("assets");

		string result = StaticAssetUrlResolver.Resolve(assets, "/my-app/assets/site.js", options);

		Assert.Equal("assets/site.8ncclf0mr4.js", result);
	}

	[Fact]
	public void Resolve_ReturnsOriginalPath_WhenStaticAssetResolutionIsDisabled()
	{
		var assets = new ResourceAssetCollection(
		[
			new ResourceAsset("dist/site.8ncclf0mr4.js", [new ResourceAssetProperty("label", "dist/site.js")])
		]);
		BundleComponentOptions options = CreateOptions("dist");
		options.ResolveStaticAssetUrls = false;

		string result = StaticAssetUrlResolver.Resolve(assets, "/dist/site.js", options);

		Assert.Equal("/dist/site.js", result);
	}

	private static BundleComponentOptions CreateOptions(params string[] staticAssetPathPrefixes)
	{
		var options = new BundleComponentOptions
		{
			StaticAssetPathPrefixes = [.. staticAssetPathPrefixes]
		};

		options.Sanitize();

		return options;
	}
}
