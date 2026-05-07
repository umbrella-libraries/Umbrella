
using System.ComponentModel;
using CommunityToolkit.Diagnostics;
using Umbrella.DynamicImage.Abstractions;
using Umbrella.Utilities.Options.Abstractions;

namespace Umbrella.WebUtilities.DynamicImage.Middleware.Options;

/// <summary>
/// Options for implementations of the DynamicImageMiddleware in the ASP.NET and ASP.NET Core projects.
/// </summary>
/// <seealso cref="IValidatableUmbrellaOptions" />
/// <seealso cref="ISanitizableUmbrellaOptions" />
public class DynamicImageMiddlewareOptions : IValidatableUmbrellaOptions, ISanitizableUmbrellaOptions
{
	private Dictionary<string, DynamicImageMiddlewareMapping> _flattenedMappings = null!;

	/// <summary>
	/// Gets or sets the mappings.
	/// </summary>
	public List<DynamicImageMiddlewareMapping> Mappings { get; set; } = null!;

	/// <summary>
	/// Gets or sets the dynamic image path prefix. Defaults to <see cref="DynamicImageConstants.DefaultPathPrefix"/>.
	/// </summary>
	public string DynamicImagePathPrefix { get; set; } = DynamicImageConstants.DefaultPathPrefix;

	/// <summary>
	/// Gets or sets a value indicating whether Jpg images should be returned in WebP or Avif format for supported browsers.
	/// Defaults to <see langword="true"/>.
	/// </summary>
	/// <remarks>Avif will be preferred over WebP where supported.</remarks>
	public bool EnableJpgPngWebPOrAvifOverride { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether URL fingerprinting is enabled for generated and canonical dynamic image URLs.
	/// Defaults to <see langword="true" />.
	/// </summary>
	public bool EnableUrlFingerprinting { get; set; } = true;

	/// <summary>
	/// Gets or sets the status code used when redirecting requests to their canonical dynamic image URL.
	/// Supported values are 301, 302, 307 and 308. Defaults to <see cref="HttpStatusCode.MovedPermanently" />.
	/// </summary>
	public HttpStatusCode CanonicalRedirectStatusCode { get; set; } = HttpStatusCode.MovedPermanently;

	/// <summary>
	/// Gets or sets the maximum concurrent resizing requests that can be processed at any one time. Defaults to 0 which means unlimited.
	/// </summary>
	public int MaxConcurrentResizingRequests { get; set; }

	/// <summary>
	/// Gets the file provider for the specified <paramref name="searchPath"/>.
	/// </summary>
	/// <param name="searchPath">The search path.</param>
	/// <returns></returns>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public DynamicImageMiddlewareMapping GetMapping(string searchPath)
	{
		Guard.IsNotNullOrWhiteSpace(searchPath, nameof(searchPath));

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

		DynamicImagePathPrefix = DynamicImagePathPrefix.Trim();
	}

	/// <inheritdoc />
	public void Validate()
	{
		Guard.IsNotNull(Mappings);
		Guard.HasSizeGreaterThan(Mappings, 0);
		Guard.IsNotNullOrWhiteSpace(DynamicImagePathPrefix);
		Guard.IsNotNull(_flattenedMappings);
		Guard.IsGreaterThan(_flattenedMappings.Count, 0);
		Guard.IsGreaterThanOrEqualTo(MaxConcurrentResizingRequests, 0);

		switch ((int)CanonicalRedirectStatusCode)
		{
			case 301:
			case 302:
			case 307:
			case 308:
				break;
			default:
				throw new ArgumentException($"{nameof(CanonicalRedirectStatusCode)} must be set to a redirect status code of 301, 302, 307 or 308.", nameof(CanonicalRedirectStatusCode));
		}
	}
}
