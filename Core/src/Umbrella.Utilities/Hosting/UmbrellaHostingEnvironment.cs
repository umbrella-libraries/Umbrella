
using System.Buffers;
using CommunityToolkit.Diagnostics;
#if NET9_0_OR_GREATER
using Microsoft.Extensions.Caching.Hybrid;
using PlatformCache = Microsoft.Extensions.Caching.Hybrid.HybridCache;
#else
using Microsoft.Extensions.Caching.Distributed;
using Umbrella.Utilities.Caching;
using PlatformCache = Microsoft.Extensions.Caching.Distributed.IDistributedCache;
#endif
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Umbrella.Utilities.Caching.Abstractions;
using Umbrella.Utilities.Exceptions;
using Umbrella.Utilities.Hosting.Abstractions;
using Umbrella.Utilities.Hosting.Options;

namespace Umbrella.Utilities.Hosting;

/// <summary>
/// Serves as the base class for all hosting environment implementations.
/// </summary>
/// <seealso cref="IUmbrellaHostingEnvironment" />
public abstract class UmbrellaHostingEnvironment : IUmbrellaHostingEnvironment
{
	#region Protected Properties
	/// <summary>
	/// Gets the log.
	/// </summary>
	protected ILogger Logger { get; }

	/// <summary>
	/// Gets the options.
	/// </summary>
	protected UmbrellaHostingEnvironmentOptions Options { get; }

	/// <summary>
	/// Gets the cache.
	/// </summary>
	protected PlatformCache Cache { get; }

	/// <summary>
	/// Gets the cache key utility.
	/// </summary>
	protected ICacheKeyUtility CacheKeyUtility { get; }

	/// <summary>
	/// Gets or sets the file provider.
	/// </summary>
	/// <remarks>Exposed as internal for unit testing / benchmarking mocks</remarks>
	protected internal Lazy<IFileProvider> FileProvider { get; set; } = null!;
	#endregion

	#region Constructors		
	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaHostingEnvironment"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="options">The options.</param>
	/// <param name="cache">The cache.</param>
	/// <param name="cacheKeyUtility">The cache key utility.</param>
	protected UmbrellaHostingEnvironment(
		ILogger logger,
		UmbrellaHostingEnvironmentOptions options,
		PlatformCache cache,
		ICacheKeyUtility cacheKeyUtility)
	{
		Logger = logger;
		Options = options;
		Cache = cache;
		CacheKeyUtility = cacheKeyUtility;
	}
	#endregion

	#region IUmbrellaHostingEnvironment Members
	/// <inheritdoc />
	public abstract string? MapPath(string virtualPath);

	/// <inheritdoc />
	public virtual async Task<string?> GetFileContentAsync(string virtualPath, bool cache = true, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNullOrWhiteSpace(virtualPath, nameof(virtualPath));

		try
		{
			return await GetFileContentAsync("Standard", FileProvider.Value, virtualPath, cache, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { virtualPath, cache }))
		{
			throw new UmbrellaException("There has been a problem reading the contents of the specified file.", exc);
		}
	}
	#endregion

	#region Protected Methods		
	/// <summary>
	/// Gets the string content of the file at the specified virtual path.
	/// </summary>
	/// <param name="fileProviderKey">The file provider key.</param>
	/// <param name="fileProvider">The file provider.</param>
	/// <param name="virtualPath">The virtual path.</param>
	/// <param name="cache">Specifies if the content should be cached.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The file content.</returns>
	protected virtual async Task<string?> GetFileContentAsync(string fileProviderKey, IFileProvider fileProvider, string virtualPath, bool cache = true, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(fileProvider, nameof(fileProvider));
		Guard.IsNotNullOrWhiteSpace(virtualPath, nameof(virtualPath));

		string[]? cacheKeyParts = null;

		try
		{
			cacheKeyParts = ArrayPool<string>.Shared.Rent(4);
			cacheKeyParts[0] = virtualPath;
			cacheKeyParts[1] = cache.ToString();
			cacheKeyParts[2] = fileProviderKey;

			string key = CacheKeyUtility.Create<UmbrellaHostingEnvironment>(cacheKeyParts, 3);

			string cleanedPath = TransformPathForFileProvider(virtualPath);

			async Task<string?> CreateAsync(CancellationToken token)
			{
				IFileInfo fileInfo = fileProvider.GetFileInfo(cleanedPath);

				if (fileInfo.Exists)
				{
					using Stream fs = fileInfo.CreateReadStream();
					using var sr = new StreamReader(fs);

#if NET8_0_OR_GREATER
					return await sr.ReadToEndAsync(token).ConfigureAwait(false);
#else
					return await sr.ReadToEndAsync().ConfigureAwait(false);
#endif
				}

				return null;
			}

			if (!cache || !Options.CacheEnabled)
				return await CreateAsync(cancellationToken).ConfigureAwait(false);

#if NET9_0_OR_GREATER
			var options = new HybridCacheEntryOptions
			{
				Expiration = Options.CacheTimeout,
				LocalCacheExpiration = Options.CacheTimeout,
				Flags = Options.CacheEntryFlags
			};

			return await Cache.GetOrCreateAsync(key, async token => await CreateAsync(token).ConfigureAwait(false), options, cancellationToken: cancellationToken).ConfigureAwait(false);
#else
			(string? item, _) = await Cache.GetOrCreateAsync(
				key,
				async () => await CreateAsync(cancellationToken).ConfigureAwait(false),
				() => new DistributedCacheEntryOptions().SetAbsoluteExpiration(Options.CacheTimeout),
				cancellationToken: cancellationToken).ConfigureAwait(false);

			return item;
#endif
		}
		finally
		{
			if (cacheKeyParts is not null)
				ArrayPool<string>.Shared.Return(cacheKeyParts);
		}
	}

	/// <summary>
	/// Transforms the path for use with an <see cref="IFileProvider"/>.
	/// </summary>
	/// <param name="virtualPath">The virtual path.</param>
	/// <returns>The transformed path.</returns>
	protected abstract string TransformPathForFileProvider(string virtualPath);
	#endregion
}
