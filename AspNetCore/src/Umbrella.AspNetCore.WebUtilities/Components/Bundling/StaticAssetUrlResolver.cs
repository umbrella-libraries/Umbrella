#if NET9_0_OR_GREATER
using Microsoft.AspNetCore.Components;
using Umbrella.AspNetCore.WebUtilities.Components.Options;
#endif

namespace Umbrella.AspNetCore.WebUtilities.Components.Bundling;

internal static class StaticAssetUrlResolver
{
#if NET9_0_OR_GREATER
	public static string Resolve(ResourceAssetCollection assets, string path, BundleComponentOptions options)
	{
		if (!options.ResolveStaticAssetUrls || string.IsNullOrWhiteSpace(path))
			return path;

		string? assetKey = NormalizeAssetKey(path, options.StaticAssetPathPrefixes);

		if (string.IsNullOrWhiteSpace(assetKey))
			return path;

		string resolvedPath = assets[assetKey];

		return resolvedPath.Equals(assetKey, StringComparison.Ordinal) ? path : resolvedPath;
	}

	internal static string? NormalizeAssetKey(string path, IReadOnlyCollection<string> staticAssetPathPrefixes)
	{
		if (string.IsNullOrWhiteSpace(path))
			return null;

		if (Uri.TryCreate(path, UriKind.Absolute, out Uri? uri) && !string.IsNullOrWhiteSpace(uri.Scheme))
			return null;

		int queryStringIndex = path.IndexOfAny(['?', '#']);

		if (queryStringIndex >= 0)
			path = path[..queryStringIndex];

		path = path.Trim().TrimStart('~', '/', '\\').Replace('\\', '/');

		if (path.Length is 0)
			return null;

		foreach (string pathPrefix in staticAssetPathPrefixes)
		{
			int pathPrefixIndex = path.IndexOf(pathPrefix, StringComparison.OrdinalIgnoreCase);

			if (pathPrefixIndex > 0)
			{
				path = path[pathPrefixIndex..];
				break;
			}
		}

		return path;
	}
#endif
}
