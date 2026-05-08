using Umbrella.FileSystem.Abstractions;

namespace Umbrella.DynamicImage.Abstractions.Caching;

/// <summary>
/// An authorization handler for files stored in the Dynamic Image cache directory.
/// </summary>
public sealed class UmbrellaDynamicImageCacheFileAuthorizationHandler : IUmbrellaFileAuthorizationHandler
{
	private readonly DynamicImageCacheCoreOptions _cacheCoreOptions;

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaDynamicImageCacheFileAuthorizationHandler"/> class.
	/// </summary>
	/// <param name="cacheCoreOptions">The cache core options.</param>
	public UmbrellaDynamicImageCacheFileAuthorizationHandler(DynamicImageCacheCoreOptions cacheCoreOptions)
	{
		_cacheCoreOptions = cacheCoreOptions;
	}

	/// <inheritdoc/>
	public string DirectoryName => _cacheCoreOptions.DirectoryName;

	/// <inheritdoc/>
	public Task<bool> AuthorizeAsync(IUmbrellaFileInfo fileInfo, UmbrellaFileOperationType operationType, CancellationToken cancellationToken = default) => Task.FromResult(true);
}
