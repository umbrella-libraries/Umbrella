using Umbrella.FileSystem.Abstractions;

namespace Umbrella.AspNetCore.WebUtilities.DynamicImage;

/// <summary>Server-only focal approval keys. Persist these keys and share them across application instances.</summary>
public sealed class DynamicImageFocalPointSigningOptions
{
	/// <summary>Gets or sets the identifier of the key used to issue approvals.</summary>
	public string ActiveKeyId { get; set; } = "";
	/// <summary>Gets or sets base64 encoded keys indexed by identifier. Each key must contain at least 32 random bytes.</summary>
	public IReadOnlyDictionary<string, string> Keys { get; set; } = new Dictionary<string, string>();
	/// <summary>Gets or sets the source URL prefix removed by the consuming image components. Defaults to <c>/files</c>, matching the renderers. Set null or empty to disable stripping.</summary>
	public string? StripPrefix { get; set; } = "/" + UmbrellaFileSystemConstants.DefaultWebFilesDirectoryName;
}
