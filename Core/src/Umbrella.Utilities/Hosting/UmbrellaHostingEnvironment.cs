
using System.Buffers;
using CommunityToolkit.Diagnostics;
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
	protected IHybridCache Cache { get; }

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
		IHybridCache cache,
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
	public virtual async Task<string?> GetFileContentAsync(string virtualPath, bool cache = true, bool watch = true, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNullOrWhiteSpace(virtualPath, nameof(virtualPath));

		try
		{
			return await GetFileContentAsync("Standard", FileProvider.Value, virtualPath, cache, watch, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { virtualPath, cache, watch }))
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
	/// <param name="watch">Specifies if the file should be watched for changes.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The file content.</returns>
	protected virtual async Task<string?> GetFileContentAsync(string fileProviderKey, IFileProvider fileProvider, string virtualPath, bool cache = true, bool watch = true, CancellationToken cancellationToken = default)
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
			cacheKeyParts[2] = watch.ToString();
			cacheKeyParts[3] = fileProviderKey;

			string key = CacheKeyUtility.Create<UmbrellaHostingEnvironment>(cacheKeyParts, 4);

			string cleanedPath = TransformPathForFileProvider(virtualPath);

			return await Cache.GetOrCreateAsync(key, async () =>
			{
				IFileInfo fileInfo = fileProvider.GetFileInfo(cleanedPath);

				if (fileInfo.Exists)
				{
					using Stream fs = fileInfo.CreateReadStream();
					using var sr = new StreamReader(fs);

					return await sr.ReadToEndAsync().ConfigureAwait(false);
				}

				return null;
			},
			Options,
			() => watch ? new[] { FileProvider.Value.Watch(cleanedPath) } : null,
			cancellationToken)
				.ConfigureAwait(false);
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