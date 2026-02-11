
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Umbrella.Utilities.Options;
using Umbrella.Utilities.Options.Abstractions;

namespace Umbrella.WebUtilities.Bundling.Options;

/// <summary>
/// Options for the <see cref="BundleUtility"/>
/// </summary>
/// <seealso cref="CacheableUmbrellaOptions" />
/// <seealso cref="ISanitizableUmbrellaOptions" />
/// <seealso cref="IValidatableUmbrellaOptions" />
public class BundleUtilityOptions : CacheableUmbrellaOptions, ISanitizableUmbrellaOptions, IValidatableUmbrellaOptions, IDevelopmentModeUmbrellaOptions
{
	/// <summary>
	/// Gets or sets the default bundle folder path relative to the web root of the application.
	/// Defaults to '/dist'.
	/// </summary>
	public string DefaultBundleFolderAppRelativePath { get; set; } = "/dist";

	/// <summary>
	/// Gets or sets a value indicating whether source files will be watched for changes.
	/// This is useful during development when files are constantly changing and should be disabled in production.
	/// Defaults to <see langword="false"/>.
	/// </summary>
	public bool WatchFiles { get; set; }

	/// <summary>
	/// Gets or sets whether a version should be appended to generated bundle URLs.
	/// This defaults to <see langword="null"/> because version appending behaviour is delegated to <see cref="BundleUtility"/> implementations.
	/// However, this can be used as a global override to explicitly enable or disable this behaviour.
	/// </summary>
	public bool? AppendVersion { get; set; }

	/// <summary>
	/// Gets or sets the cache priority when caching items in memory. Defaults to <see cref="F:Microsoft.Extensions.Caching.Memory.CacheItemPriority.NeverRemove" />.
	/// </summary>
	public override CacheItemPriority CachePriority { get; set; } = CacheItemPriority.NeverRemove;

	/// <inheritdoc />
	public virtual void Sanitize()
	{
		// Ensure the path ends with a slash
		if (!DefaultBundleFolderAppRelativePath?.EndsWith("/", StringComparison.Ordinal) is true)
			DefaultBundleFolderAppRelativePath += "/";
	}

	/// <inheritdoc />
	public virtual void Validate() => Guard.IsNotNullOrWhiteSpace(DefaultBundleFolderAppRelativePath);
	void IDevelopmentModeUmbrellaOptions.SetDevelopmentMode(bool isDevelopmentMode) => WatchFiles = isDevelopmentMode;
}