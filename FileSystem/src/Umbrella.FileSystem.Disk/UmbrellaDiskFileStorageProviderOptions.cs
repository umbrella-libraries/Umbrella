
using CommunityToolkit.Diagnostics;
using Umbrella.FileSystem.Abstractions;
using Umbrella.Utilities.Helpers;
using Umbrella.Utilities.Options.Abstractions;

namespace Umbrella.FileSystem.Disk;

/// <summary>
/// Options for the <see cref="UmbrellaDiskFileStorageProvider"/>.
/// </summary>
/// <seealso cref="UmbrellaFileStorageProviderOptionsBase" />
/// <seealso cref="ISanitizableUmbrellaOptions" />
/// <seealso cref="IValidatableUmbrellaOptions" />
public class UmbrellaDiskFileStorageProviderOptions : UmbrellaFileStorageProviderOptionsBase, ISanitizableUmbrellaOptions, IValidatableUmbrellaOptions
{
	/// <summary>
	/// Gets or sets the physical root path on disk for the file provider.
	/// </summary>
	public string RootPhysicalPath { get; set; } = null!;

	/// <summary>
	/// Sanitizes this instance.
	/// </summary>
	public void Sanitize() => RootPhysicalPath = PathHelper.PlatformNormalize(RootPhysicalPath.Trim().TrimEnd('\\'));

	/// <summary>
	/// Validates this instance.
	/// </summary>
	public void Validate() => Guard.IsNotNullOrWhiteSpace(RootPhysicalPath, nameof(RootPhysicalPath));
}