using CommunityToolkit.Diagnostics;
using Umbrella.Utilities.Options.Abstractions;

namespace Umbrella.AspNetCore.WebUtilities.Components.Options;

/// <summary>
/// Options for bundle Razor components.
/// </summary>
public class BundleComponentOptions : ISanitizableUmbrellaOptions, IValidatableUmbrellaOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether bundle component URLs should be resolved through the static web asset collection.
	/// </summary>
	public bool ResolveStaticAssetUrls { get; set; } = true;

	/// <summary>
	/// Gets the static asset path prefixes used to strip an application path base before resolving bundle URLs.
	/// </summary>
	/// <remarks>
	/// Defaults to <c>dist</c>. Add or replace values when the consuming application emits bundles to a different path,
	/// e.g. <c>assets</c> or <c>build</c>.
	/// </remarks>
	public List<string> StaticAssetPathPrefixes { get; set; } = ["dist"];

	/// <inheritdoc/>
	public void Sanitize()
	{
		Guard.IsNotNull(StaticAssetPathPrefixes);

		for (int i = StaticAssetPathPrefixes.Count - 1; i >= 0; i--)
		{
			string? pathPrefix = StaticAssetPathPrefixes[i];

			if (string.IsNullOrWhiteSpace(pathPrefix))
			{
				StaticAssetPathPrefixes.RemoveAt(i);
				continue;
			}

			pathPrefix = pathPrefix.Trim().Replace('\\', '/').Trim('~', '/');

			StaticAssetPathPrefixes[i] = pathPrefix.EndsWith("/", StringComparison.Ordinal) ? pathPrefix : $"{pathPrefix}/";
		}
	}

	/// <inheritdoc/>
	public void Validate()
	{
		Guard.IsNotNull(StaticAssetPathPrefixes);
	}
}
