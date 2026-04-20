using CommunityToolkit.Diagnostics;
using Microsoft.Graph;
using Umbrella.FileSystem.Abstractions;
using Umbrella.Utilities.Options.Abstractions;

namespace Umbrella.FileSystem.SharePoint;

/// <summary>
/// Options for the <see cref="UmbrellaSharePointFileStorageProvider"/>.
/// </summary>
public class UmbrellaSharePointFileStorageProviderOptions : UmbrellaFileStorageProviderOptionsBase, ISanitizableUmbrellaOptions, IValidatableUmbrellaOptions
{
	/// <summary>
	/// The Microsoft Graph site ID for the SharePoint site, e.g. <c>contoso.sharepoint.com:/sites/MySite:</c>.
	/// </summary>
	public string SiteId { get; set; } = null!;

	/// <summary>
	/// The name of the SharePoint document library, e.g. <c>Shared Documents</c>.
	/// </summary>
	public string DriveName { get; set; } = null!;

	/// <summary>
	/// The <see cref="Microsoft.Graph.GraphServiceClient"/> used to call the Microsoft Graph API.
	/// Assign this in the options builder, typically by resolving a keyed service from the DI container.
	/// </summary>
	public GraphServiceClient GraphServiceClient { get; set; } = null!;

	/// <summary>
	/// An optional delegate that translates a sanitized logical subpath (leading <c>/</c>, lowercase)
	/// into the SharePoint drive-relative path (no leading <c>/</c>) to use in Graph API calls.
	/// If <see langword="null"/>, the default behaviour strips the leading <c>/</c> only.
	/// </summary>
	/// <example>
	/// Map <c>/files/profile-general-documents/{groupId}/{file}</c>
	/// → <c>General Documents_{groupId}/{file}</c>:
	/// <code>
	/// options.SubPathTranslator = path => {
	///     var parts = path.TrimStart('/').Split('/');
	///     if (parts.Length >= 4 &amp;&amp; parts[0] == "files" &amp;&amp; parts[1] == "profile-general-documents")
	///         return $"General Documents_{parts[2]}/{string.Join("/", parts[3..])}";
	///     return path.TrimStart('/');
	/// };
	/// </code>
	/// </example>
	public Func<string, string>? SubPathTranslator { get; set; }

	/// <summary>
	/// An optional delegate that translates a SharePoint drive-relative item path back into
	/// the logical subpath (with leading <c>/</c>) used by the Umbrella file system.
	/// Required for <c>EnumerateDirectoryAsync</c> to return correct <c>SubPath</c> values.
	/// If <see langword="null"/>, the default behaviour prepends <c>/</c> only.
	/// </summary>
	public Func<string, string>? SubPathReverseTranslator { get; set; }

	/// <inheritdoc />
	public void Sanitize()
	{
		SiteId = SiteId?.Trim()!;
		DriveName = DriveName?.Trim()!;
	}

	/// <inheritdoc />
	public void Validate()
	{
		Guard.IsNotNullOrWhiteSpace(SiteId);
		Guard.IsNotNullOrWhiteSpace(DriveName);
		Guard.IsNotNull(GraphServiceClient);
	}
}
