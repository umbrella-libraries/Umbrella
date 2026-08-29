using CommunityToolkit.Diagnostics;
using Umbrella.Utilities.Options;
using Umbrella.Utilities.Options.Abstractions;

namespace Umbrella.WebUtilities.Versioning.Options;

/// <summary>
/// Options for the <see cref="SystemVersionService"/>.
/// </summary>
/// <seealso cref="CacheableUmbrellaOptions" />
/// <seealso cref="ISanitizableUmbrellaOptions" />
/// <seealso cref="IValidatableUmbrellaOptions" />
public class SystemVersionServiceOptions : CacheableUmbrellaOptions, ISanitizableUmbrellaOptions, IValidatableUmbrellaOptions
{
	/// <summary>
	/// Gets or sets the version file path.
	/// </summary>
	/// <remarks>
	/// Defaults to <![CDATA[Path.Combine(Directory.GetCurrentDirectory(), "BuildSourceVersion.txt")]]>
	/// </remarks>
	public string VersionFilePath { get; set; } = Path.Combine(Directory.GetCurrentDirectory(), "BuildSourceVersion.txt");

	/// <summary>
	/// Gets or sets a value indicating whether to include database version information.
	/// </summary>
	/// <remarks>Defaults to <see langword="true" />.</remarks>
	public bool IncludeDatabaseVersion { get; set; } = true;

	/// <inheritdoc />
	public void Sanitize() => VersionFilePath = VersionFilePath.Trim();

	/// <inheritdoc />
	public void Validate() => Guard.IsNotNullOrEmpty(VersionFilePath);
}
