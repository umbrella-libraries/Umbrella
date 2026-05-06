
using System.ComponentModel;
using CommunityToolkit.Diagnostics;
using Umbrella.Utilities.Options.Abstractions;
using Umbrella.WebUtilities.FileSystem.Constants;

namespace Umbrella.WebUtilities.FileSystem.Middleware.Options;

/// <summary>
/// Options for implementations of the FileSystemMiddleware in the ASP.NET and ASP.NET Core projects.
/// </summary>
/// <seealso cref="ISanitizableUmbrellaOptions" />
/// <seealso cref="IValidatableUmbrellaOptions" />
public class FileSystemMiddlewareOptions : ISanitizableUmbrellaOptions, IValidatableUmbrellaOptions
{
	private List<KeyValuePair<string, FileSystemMiddlewareMapping>>? _flattenedMappings;

	/// <summary>
	/// Gets or sets the mappings.
	/// </summary>
	public List<FileSystemMiddlewareMapping>? Mappings { get; set; }

	/// <summary>
	/// Gets or sets the file system path prefix. Defaults to <see cref="FileSystemConstants.DefaultPathPrefix"/>.
	/// </summary>
	public string FileSystemPathPrefix { get; set; } = FileSystemConstants.DefaultPathPrefix;

	/// <summary>
	/// Gets the file provider for the specified <paramref name="searchPath"/>.
	/// </summary>
	/// <param name="searchPath">The search path.</param>
	/// <returns></returns>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public FileSystemMiddlewareMapping? GetMapping(string searchPath)
	{
		Guard.IsNotNullOrWhiteSpace(searchPath);

		if (_flattenedMappings is null)
			return null;

		string normalizedSearchPath = searchPath.Trim();

		foreach (KeyValuePair<string, FileSystemMiddlewareMapping> mapping in _flattenedMappings)
		{
			if (normalizedSearchPath.StartsWith(mapping.Key, StringComparison.OrdinalIgnoreCase))
				return mapping.Value;
		}

		return null;
	}

	/// <inheritdoc />
	public void Sanitize()
	{
		if (Mappings is not null)
		{
			Mappings.ForEach(x => x.Sanitize());
			_flattenedMappings =
			[
				.. Mappings
					.SelectMany(x => x.FileProviderMapping.AppRelativeFolderPaths.Select(y => new KeyValuePair<string, FileSystemMiddlewareMapping>(y, x)))
					.OrderByDescending(x => x.Key.Length)
			];
		}

		FileSystemPathPrefix = FileSystemPathPrefix.Trim();
	}

	/// <inheritdoc />
	public void Validate()
	{
		Guard.IsNotNull(Mappings);
		Guard.HasSizeGreaterThan(Mappings, 0);
		Guard.IsNotNullOrWhiteSpace(FileSystemPathPrefix);
		Guard.IsNotNull(_flattenedMappings);
		Guard.IsGreaterThan(_flattenedMappings.Count, 0);

		string[] duplicatePaths = _flattenedMappings
			.GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
			.Where(x => x.Count() > 1)
			.Select(x => x.Key)
			.ToArray();

		if (duplicatePaths.Length > 0)
			throw new ArgumentException($"Duplicate app relative folder paths are not permitted: {string.Join(", ", duplicatePaths)}.", nameof(Mappings));

		Mappings?.ForEach(x => x.Validate());
	}
}
