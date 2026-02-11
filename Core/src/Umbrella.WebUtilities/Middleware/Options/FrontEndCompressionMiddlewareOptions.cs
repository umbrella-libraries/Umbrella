


using System.ComponentModel;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.FileProviders;
using Umbrella.Utilities.Extensions;
using Umbrella.Utilities.Options.Abstractions;

namespace Umbrella.WebUtilities.Middleware.Options;

/// <summary>
/// Options for implementations of the FrontEndCompressionMiddleware in the ASP.NET and ASP.NET Core projects.
/// </summary>
/// <seealso cref="ISanitizableUmbrellaOptions" />
/// <seealso cref="IValidatableUmbrellaOptions" />
public class FrontEndCompressionMiddlewareOptions : ISanitizableUmbrellaOptions, IValidatableUmbrellaOptions, IDevelopmentModeUmbrellaOptions
{
	private Dictionary<string, FrontEndCompressionMiddlewareMapping>? _flattenedMappings;

	/// <summary>
	/// Gets or sets the mappings.
	/// </summary>
	public List<FrontEndCompressionMiddlewareMapping> Mappings { get; set; } =
	[
		new FrontEndCompressionMiddlewareMapping
		{
			AppRelativeFolderPaths = ["/dist"],
			WatchFiles = false
		}
	];

	/// <summary>
	/// Gets or sets the Accept-Encoding header key. Defaults to "Accept-Encoding".
	/// This is here to allow the header key to be altered when requests go via a proxy.
	/// </summary>
	public string AcceptEncodingHeaderKey { get; set; } = "Accept-Encoding";

	/// <summary>
	/// Gets or sets a transformation applied to the "Accept-Encoding" header values based on the headers of the current request.
	/// This is useful for last resort scenarios where, e.g. User Agent sniffing is needed to refine the encoding values.
	/// </summary>
	public Action<IReadOnlyDictionary<string, IEnumerable<string?>>, HashSet<string>>? AcceptEncodingModifier { get; set; }

	/// <summary>
	/// Gets or sets the buffer size in bytes when copying data between streams during compression. Defaults to 81920.
	/// </summary>
	public int BufferSizeBytes { get; set; } = 81920;

	/// <summary>
	/// Gets or sets the optional response cache determiner. Used to perform additional checks to see if the response should have caching headers applied.
	/// </summary>
	public Func<IFileInfo, bool>? ResponseCacheDeterminer { get; set; }

	/// <summary>
	/// Gets the file provider for the specified <paramref name="searchPath"/>.
	/// </summary>
	/// <param name="searchPath">The search path.</param>
	/// <returns></returns>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public FrontEndCompressionMiddlewareMapping? GetMapping(string searchPath)
	{
		Guard.IsNotNullOrWhiteSpace(searchPath, nameof(searchPath));

		return _flattenedMappings?.SingleOrDefault(x => searchPath.Trim().StartsWith(x.Key, StringComparison.OrdinalIgnoreCase)).Value;
	}

	/// <summary>
	/// Sanitizes this instance.
	/// </summary>
	public void Sanitize()
	{
		AcceptEncodingHeaderKey = AcceptEncodingHeaderKey.TrimToLowerInvariant();

		if (Mappings is not null)
		{
			Mappings.ForEach(x => x.Sanitize());
			_flattenedMappings = Mappings.SelectMany(x => x.AppRelativeFolderPaths.ToDictionary(y => y, y => x)).ToDictionary(x => x.Key, x => x.Value);
		}
	}

	/// <summary>
	/// Validates this instance.
	/// </summary>
	public void Validate()
	{
		Guard.IsNotNullOrWhiteSpace(AcceptEncodingHeaderKey, nameof(AcceptEncodingHeaderKey));
		Guard.IsGreaterThanOrEqualTo(BufferSizeBytes, 1);
	}

	void IDevelopmentModeUmbrellaOptions.SetDevelopmentMode(bool isDevelopmentMode) => Mappings.ForEach(x => x.WatchFiles = isDevelopmentMode);
}