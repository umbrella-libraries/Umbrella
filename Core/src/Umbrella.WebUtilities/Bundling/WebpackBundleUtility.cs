
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Umbrella.Utilities;
using Umbrella.Utilities.Caching.Abstractions;
using Umbrella.WebUtilities.Bundling.Abstractions;
using Umbrella.WebUtilities.Bundling.Options;
using Umbrella.WebUtilities.Hosting;

namespace Umbrella.WebUtilities.Bundling;

/// <summary>
/// A utility for resolving named CSS or JS Webpack bundles.
/// </summary>
/// <seealso cref="BundleUtility{WebpackBundleUtilityOptions}" />
/// <seealso cref="IWebpackBundleUtility" />
public class WebpackBundleUtility : BundleUtility<WebpackBundleUtilityOptions>, IWebpackBundleUtility
{
	/// <summary>
	/// Gets the file provider.
	/// </summary>
	/// <remarks>Exposed as internal for unit testing purposes.</remarks>
	protected internal IFileProvider FileProvider { get; internal set; }

	/// <summary>
	/// Initializes a new instance of the <see cref="WebpackBundleUtility"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="options">The options.</param>
	/// <param name="hybridCache">The hybrid cache.</param>
	/// <param name="hostingEnvironment">The hosting environment.</param>
	/// <param name="cacheKeyUtility">The cache key utility.</param>
	public WebpackBundleUtility(
		ILogger<WebpackBundleUtility> logger,
		WebpackBundleUtilityOptions options,
		IHybridCache hybridCache,
		ICacheKeyUtility cacheKeyUtility,
		IUmbrellaWebHostingEnvironment hostingEnvironment)
		: base(logger, options, hybridCache, cacheKeyUtility, hostingEnvironment)
	{
		string? rootPath = hostingEnvironment.MapPath(Options.DefaultBundleFolderAppRelativePath, false);
		Guard.IsNotNullOrWhiteSpace(rootPath);

		FileProvider = new PhysicalFileProvider(rootPath!);
	}

	#region Overridden Methods				
	/// <summary>
	/// Gets the path to the named script bundle or path.
	/// </summary>
	/// <param name="bundleNameOrPath">The bundle name or path.</param>
	/// <param name="cancellationToken">The <see cref="CancellationToken" />.</param>
	/// <returns>
	/// The application relative path to the bundle.
	/// </returns>
	/// <exception cref="UmbrellaWebException">There has been a problem resolving the path to the Webpack bundle.</exception>
	public override async Task<string> GetScriptPathAsync(string bundleNameOrPath, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNullOrWhiteSpace(bundleNameOrPath);

		try
		{
			string cacheKey = CacheKeyUtility.Create<WebpackBundleUtility>($"{bundleNameOrPath}:js");

			return await Cache.GetOrCreateAsync(cacheKey,
				async () => await ResolveBundlePathAsync(bundleNameOrPath, "js", false, cancellationToken).ConfigureAwait(false),
				Options,
				cancellationToken: cancellationToken)
				.ConfigureAwait(false);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { bundleNameOrPath }))
		{
			throw new UmbrellaWebException("There has been a problem resolving the path to the Webpack bundle.", exc);
		}
	}

	/// <summary>
	/// Gets the path to the named css bundle or path.
	/// </summary>
	/// <param name="bundleNameOrPath">The bundle name or path.</param>
	/// <param name="cancellationToken">The <see cref="CancellationToken" />.</param>
	/// <returns>
	/// The application relative path to the bundle.
	/// </returns>
	/// <exception cref="UmbrellaWebException">There has been a problem resolving the path to the Webpack bundle.</exception>
	public override async Task<string> GetStyleSheetPathAsync(string bundleNameOrPath, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNullOrWhiteSpace(bundleNameOrPath);

		try
		{
			string cacheKey = CacheKeyUtility.Create<WebpackBundleUtility>($"{bundleNameOrPath}:css");

			return await Cache.GetOrCreateAsync(cacheKey,
				async () => await ResolveBundlePathAsync(bundleNameOrPath, "css", false, cancellationToken).ConfigureAwait(false),
				Options,
				cancellationToken: cancellationToken)
				.ConfigureAwait(false);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { bundleNameOrPath }))
		{
			throw new UmbrellaWebException("There has been a problem resolving the path to the Webpack bundle.", exc);
		}
	}

	/// <summary>
	/// Determines the bundle path asynchronous.
	/// </summary>
	/// <param name="bundleNameOrPath">The bundle name or path.</param>
	/// <param name="bundleType">Type of the bundle.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The path to the bundle.</returns>
	/// <exception cref="ArgumentException">The path must not be app relative. It should just be the webpack bundle name.</exception>
	/// <exception cref="Exception">The specified Webpack bundle cannot be found.</exception>
	protected override async Task<string> DetermineBundlePathAsync(string bundleNameOrPath, string bundleType, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		if (bundleNameOrPath.StartsWith("~", StringComparison.Ordinal) || bundleNameOrPath.StartsWith("/", StringComparison.Ordinal))
			throw new ArgumentException("The path must not be app relative. It should just be the webpack bundle name.");

		if (Path.HasExtension(bundleNameOrPath))
			bundleNameOrPath = Path.GetFileNameWithoutExtension(bundleNameOrPath);

		string bundleNameKey = (bundleNameOrPath + "." + bundleType).ToLowerInvariant();

		Dictionary<string, string> dicManifest = await GetManifestAsync(cancellationToken);

		return dicManifest.TryGetValue(bundleNameKey, out string? bundlePath)
			? bundlePath
			: throw new InvalidOperationException("The specified Webpack bundle cannot be found.");
	}
	#endregion

	private async Task<Dictionary<string, string>> GetManifestAsync(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			string cacheKey = CacheKeyUtility.Create<WebpackBundleUtility>("WebpackManifest");

			return await Cache.GetOrCreateAsync(cacheKey, async () =>
			{
				IFileInfo fileInfo = FileProvider.GetFileInfo(Options.ManifestJsonFileSubPath);

				if (!fileInfo.Exists)
					throw new InvalidOperationException("The specified Webpack Manifest JSON file does not exist.");

				using Stream stream = fileInfo.CreateReadStream();
				using var sr = new StreamReader(stream);
				string json = await sr.ReadToEndAsync().ConfigureAwait(false);

				var deserializedJson = UmbrellaStatics.DeserializeJson<Dictionary<string, string>>(json);

				if (deserializedJson is null)
					throw new InvalidOperationException("There has been a problem deserializing the json.");

				return deserializedJson.ToDictionary(x => x.Key.ToLowerInvariant(), x =>
				{
					string loweredPath = x.Value.ToLowerInvariant();

					int idxLastSlash = loweredPath.LastIndexOf('/');

					if (idxLastSlash != -1)
						loweredPath = loweredPath[(idxLastSlash + 1)..];

					return Path.Combine(Options.DefaultBundleFolderAppRelativePath, loweredPath);
				});
			},
			Options, expirationTokensBuilder: () => Options.WatchFiles ? new[] { FileProvider.Watch(Options.ManifestJsonFileSubPath) } : null,
			cancellationToken: cancellationToken)
				.ConfigureAwait(false);
		}
		catch (Exception exc)
		{
			throw new UmbrellaWebException("There has been a problem reading the Webpack Manifest JSON.", exc);
		}
	}
}