
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Logging;
#if NET9_0_OR_GREATER
using Microsoft.Extensions.Caching.Hybrid;
using PlatformCache = Microsoft.Extensions.Caching.Hybrid.HybridCache;
#else
using Microsoft.Extensions.Caching.Distributed;
using Umbrella.Utilities.Caching;
using PlatformCache = Microsoft.Extensions.Caching.Distributed.IDistributedCache;
#endif
using Umbrella.Utilities.Caching.Abstractions;
using Umbrella.WebUtilities.Bundling.Abstractions;
using Umbrella.WebUtilities.Bundling.Options;
using Umbrella.WebUtilities.Hosting;

namespace Umbrella.WebUtilities.Bundling;

/// <summary>
/// A utility for resolving named CSS or JS bundles or relative paths to such bundles.
/// </summary>
public class BundleUtility : BundleUtility<BundleUtilityOptions>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="BundleUtility"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="options">The options.</param>
	/// <param name="cache">The platform cache.</param>
	/// <param name="cacheKeyUtility">The cache key utility.</param>
	/// <param name="hostingEnvironment">The hosting environment.</param>
	public BundleUtility(
		ILogger<BundleUtility> logger,
		BundleUtilityOptions options,
		PlatformCache cache,
		ICacheKeyUtility cacheKeyUtility,
		IUmbrellaWebHostingEnvironment hostingEnvironment)
		: base(logger, options, cache, cacheKeyUtility, hostingEnvironment)
	{
	}
}

/// <summary>
/// An abstract class which serves as the base class for both the <see cref="BundleUtility"/> and <see cref="WebpackBundleUtility"/> types.
/// </summary>
public abstract class BundleUtility<TOptions> : IBundleUtility
	where TOptions : BundleUtilityOptions
{
	#region Protected Properties
	/// <summary>
	/// Gets the logger.
	/// </summary>
	protected ILogger Logger { get; }

	/// <summary>
	/// Gets the <typeparamref name="TOptions"/> being used.
	/// </summary>
	protected TOptions Options { get; }

	/// <summary>
	/// Gets the cache.
	/// </summary>
	protected PlatformCache Cache { get; }

	/// <summary>
	/// Gets the cache key utility.
	/// </summary>
	protected ICacheKeyUtility CacheKeyUtility { get; }

	/// <summary>
	/// Gets the Umbrella hosting environment.
	/// </summary>
	protected IUmbrellaWebHostingEnvironment HostingEnvironment { get; }
	#endregion

	#region Constructors		
	/// <summary>
	/// Initializes a new instance of the <see cref="BundleUtility{TOptions}"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="options">The options.</param>
	/// <param name="cache">The platform cache.</param>
	/// <param name="cacheKeyUtility">The cache key utility.</param>
	/// <param name="hostingEnvironment">The hosting environment.</param>
	protected BundleUtility(
		ILogger logger,
		TOptions options,
		PlatformCache cache,
		ICacheKeyUtility cacheKeyUtility,
		IUmbrellaWebHostingEnvironment hostingEnvironment)
	{
		Logger = logger;
		Options = options;
		Cache = cache;
		CacheKeyUtility = cacheKeyUtility;
		HostingEnvironment = hostingEnvironment;
	}
	#endregion

	#region IBundleUtility Members
	/// <summary>
	/// Gets the path to the named script bundle or path.
	/// </summary>
	/// <param name="bundleNameOrPath">The bundle name or path.</param>
	/// <param name="cancellationToken">The <see cref="CancellationToken" />.</param>
	/// <returns> The application relative path to the bundle.</returns>
	/// <exception cref="UmbrellaWebException">There has been a problem resolving the path to the bundle.</exception>
	public virtual async Task<string> GetScriptPathAsync(string bundleNameOrPath, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNullOrWhiteSpace(bundleNameOrPath);

		try
		{
			string cacheKey = CacheKeyUtility.Create<BundleUtility<TOptions>>($"{bundleNameOrPath}:js");

			if (!Options.CacheEnabled)
				return await ResolveBundlePathAsync(bundleNameOrPath, "js", true, cancellationToken).ConfigureAwait(false);

#if NET9_0_OR_GREATER
			return await Cache.GetOrCreateAsync(cacheKey, async token => await ResolveBundlePathAsync(bundleNameOrPath, "js", true, token).ConfigureAwait(false), CreateHybridCacheEntryOptions(), cancellationToken: cancellationToken).ConfigureAwait(false);
#else
			(string item, _) = await Cache.GetOrCreateAsync(
				cacheKey,
				async () => await ResolveBundlePathAsync(bundleNameOrPath, "js", true, cancellationToken).ConfigureAwait(false),
				CreateDistributedCacheEntryOptions,
				cancellationToken: cancellationToken).ConfigureAwait(false);

			return item;
#endif
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { bundleNameOrPath }))
		{
			throw new UmbrellaWebException("There has been a problem resolving the path to the bundle.", exc);
		}
	}

	/// <summary>
	/// Gets the script content at the named bundle or path.
	/// </summary>
	/// <param name="bundleNameOrPath">The bundle name or path.</param>
	/// <param name="cancellationToken">The <see cref="CancellationToken" />.</param>
	/// <returns>
	/// The bundle content.
	/// </returns>
	/// <exception cref="UmbrellaWebException">There was a problem getting the script content.</exception>
	public virtual async Task<string?> GetScriptContentAsync(string bundleNameOrPath, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNullOrWhiteSpace(bundleNameOrPath, nameof(bundleNameOrPath));

		try
		{
			string cacheKey = CacheKeyUtility.Create<BundleUtility<TOptions>>($"{bundleNameOrPath}:js-content");

			if (!Options.CacheEnabled)
				return await ResolveBundleContentAsync(bundleNameOrPath, "js", cancellationToken).ConfigureAwait(false);

#if NET9_0_OR_GREATER
			return await Cache.GetOrCreateAsync(cacheKey, async token => await ResolveBundleContentAsync(bundleNameOrPath, "js", token).ConfigureAwait(false), CreateHybridCacheEntryOptions(), cancellationToken: cancellationToken).ConfigureAwait(false);
#else
			(string? item, _) = await Cache.GetOrCreateAsync(
				cacheKey,
				async () => await ResolveBundleContentAsync(bundleNameOrPath, "js", cancellationToken).ConfigureAwait(false),
				CreateDistributedCacheEntryOptions,
				cancellationToken: cancellationToken).ConfigureAwait(false);

			return item;
#endif
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { bundleNameOrPath }))
		{
			throw new UmbrellaWebException("There was a problem getting the script content.", exc);
		}
	}

	/// <summary>
	/// Gets the path to the named css bundle or path.
	/// </summary>
	/// <param name="bundleNameOrPath">The bundle name or path.</param>
	/// <param name="cancellationToken">The <see cref="CancellationToken" />.</param>
	/// <returns>The application relative path to the bundle.</returns>
	/// <exception cref="UmbrellaWebException">There has been a problem resolving the path to the bundle.</exception>
	public virtual async Task<string> GetStyleSheetPathAsync(string bundleNameOrPath, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNullOrWhiteSpace(bundleNameOrPath, nameof(bundleNameOrPath));

		try
		{
			string cacheKey = CacheKeyUtility.Create<BundleUtility<TOptions>>($"{bundleNameOrPath}:css");

			if (!Options.CacheEnabled)
				return await ResolveBundlePathAsync(bundleNameOrPath, "css", true, cancellationToken).ConfigureAwait(false);

#if NET9_0_OR_GREATER
			return await Cache.GetOrCreateAsync(cacheKey, async token => await ResolveBundlePathAsync(bundleNameOrPath, "css", true, token).ConfigureAwait(false), CreateHybridCacheEntryOptions(), cancellationToken: cancellationToken).ConfigureAwait(false);
#else
			(string item, _) = await Cache.GetOrCreateAsync(
				cacheKey,
				async () => await ResolveBundlePathAsync(bundleNameOrPath, "css", true, cancellationToken).ConfigureAwait(false),
				CreateDistributedCacheEntryOptions,
				cancellationToken: cancellationToken).ConfigureAwait(false);

			return item;
#endif
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { bundleNameOrPath }))
		{
			throw new UmbrellaWebException("There has been a problem resolving the path to the bundle.", exc);
		}
	}

	/// <summary>
	/// Gets the css content at the named bundle or path.
	/// </summary>
	/// <param name="bundleNameOrPath">The bundle name or path.</param>
	/// <param name="cancellationToken">The <see cref="CancellationToken" />.</param>
	/// <returns>
	/// The bundle content.
	/// </returns>
	/// <exception cref="UmbrellaWebException">There was a problem getting the stylesheet content.</exception>
	public virtual async Task<string?> GetStyleSheetContentAsync(string bundleNameOrPath, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNullOrWhiteSpace(bundleNameOrPath, nameof(bundleNameOrPath));

		try
		{
			string cacheKey = CacheKeyUtility.Create<BundleUtility<TOptions>>($"{bundleNameOrPath}:css-content");

			if (!Options.CacheEnabled)
				return await ResolveBundleContentAsync(bundleNameOrPath, "css", cancellationToken).ConfigureAwait(false);

#if NET9_0_OR_GREATER
			return await Cache.GetOrCreateAsync(cacheKey, async token => await ResolveBundleContentAsync(bundleNameOrPath, "css", token).ConfigureAwait(false), CreateHybridCacheEntryOptions(), cancellationToken: cancellationToken).ConfigureAwait(false);
#else
			(string? item, _) = await Cache.GetOrCreateAsync(
				cacheKey,
				async () => await ResolveBundleContentAsync(bundleNameOrPath, "css", cancellationToken).ConfigureAwait(false),
				CreateDistributedCacheEntryOptions,
				cancellationToken: cancellationToken).ConfigureAwait(false);

			return item;
#endif
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { bundleNameOrPath }))
		{
			throw new UmbrellaWebException("There was a problem getting the stylesheet content.", exc);
		}
	}
	#endregion

	#region Protected Methods		
	/// <summary>
	/// Resolves the bundle path.
	/// </summary>
	/// <param name="bundleNameOrPath">The bundle name or path.</param>
	/// <param name="bundleType">Type of the bundle.</param>
	/// <param name="appendVersion">if set to, a version number will be appended to the path as a querystring.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The bundle path.</returns>
	protected async Task<string> ResolveBundlePathAsync(string bundleNameOrPath, string bundleType, bool appendVersion, CancellationToken cancellationToken)
		=> HostingEnvironment.MapWebPath(await DetermineBundlePathAsync(bundleNameOrPath, bundleType, cancellationToken).ConfigureAwait(false), appendVersion: Options.AppendVersion ?? appendVersion);

	/// <summary>
	/// Resolves the bundle content asynchronous.
	/// </summary>
	/// <param name="bundleNameOrPath">The bundle name or path.</param>
	/// <param name="bundleType">Type of the bundle.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The bundle content.</returns>
	protected async Task<string?> ResolveBundleContentAsync(string bundleNameOrPath, string bundleType, CancellationToken cancellationToken)
		=> await HostingEnvironment.GetFileContentAsync(await DetermineBundlePathAsync(bundleNameOrPath, bundleType, cancellationToken).ConfigureAwait(false), false, Options.CacheEnabled, cancellationToken).ConfigureAwait(false);

#if NET9_0_OR_GREATER
	private HybridCacheEntryOptions CreateHybridCacheEntryOptions()
		=> new()
		{
			Expiration = Options.CacheTimeout,
			LocalCacheExpiration = Options.CacheTimeout,
			Flags = Options.CacheEntryFlags
		};
#else
	private DistributedCacheEntryOptions CreateDistributedCacheEntryOptions()
		=> new DistributedCacheEntryOptions().SetAbsoluteExpiration(Options.CacheTimeout);
#endif

	/// <summary>
	/// Determines the bundle path asynchronous.
	/// </summary>
	/// <param name="bundleNameOrPath">The bundle name or path.</param>
	/// <param name="bundleType">Type of the bundle.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The bundle path.</returns>
	protected virtual Task<string> DetermineBundlePathAsync(string bundleNameOrPath, string bundleType, CancellationToken cancellationToken)
	{
		Guard.IsNotNull(bundleNameOrPath);

		if (Path.HasExtension(bundleNameOrPath))
			bundleNameOrPath = bundleNameOrPath[..bundleNameOrPath.LastIndexOf('.')];

		bundleNameOrPath += "." + bundleType;

		return bundleNameOrPath.StartsWith("~", StringComparison.Ordinal) || bundleNameOrPath.StartsWith("/", StringComparison.Ordinal)
			? Task.FromResult(bundleNameOrPath.ToLowerInvariant())
			: Task.FromResult(Path.Combine(Options.DefaultBundleFolderAppRelativePath, bundleNameOrPath).ToLowerInvariant());
	}
	#endregion
}
