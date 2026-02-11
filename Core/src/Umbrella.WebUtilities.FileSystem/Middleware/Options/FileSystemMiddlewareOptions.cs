
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
	private Dictionary<string, FileSystemMiddlewareMapping>? _flattenedMappings;

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

		return _flattenedMappings.SingleOrDefault(x => searchPath.Trim().StartsWith(x.Key, StringComparison.OrdinalIgnoreCase)).Value;
	}

	/// <inheritdoc />
	public void Sanitize()
	{
		if (Mappings is not null)
		{
			Mappings.ForEach(x => x.Sanitize());
			_flattenedMappings = Mappings.SelectMany(x => x.FileProviderMapping.AppRelativeFolderPaths.ToDictionary(y => y, y => x)).ToDictionary(x => x.Key, x => x.Value);
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

		Mappings?.ForEach(x => x.Validate());
	}
}