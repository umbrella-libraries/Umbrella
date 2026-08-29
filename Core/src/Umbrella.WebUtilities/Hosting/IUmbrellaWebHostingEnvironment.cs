using Umbrella.Utilities.Hosting.Abstractions;

namespace Umbrella.WebUtilities.Hosting;

/// <summary>
/// An abstraction over the current web hosting environment.
/// </summary>
/// <seealso cref="IUmbrellaHostingEnvironment" />
public interface IUmbrellaWebHostingEnvironment : IUmbrellaHostingEnvironment
{
	/// <summary>
	/// Maps the specified virtual path to an absolute path.
	/// </summary>
	/// <param name="virtualPath">The virtual path.</param>
	/// <param name="fromContentRoot">Determines whether to map the virtual path from the web (only applicable for ASP.NET Core) or the content root.</param>
	/// <returns>The mapped path.</returns>
	string? MapPath(string virtualPath, bool fromContentRoot);

	/// <summary>
	/// Maps the web path.
	/// </summary>
	/// <param name="virtualPath">The virtual path.</param>
	/// <param name="toAbsoluteUrl">if set to <c>true</c> generates an absolute URL instead of a relative application one.</param>
	/// <param name="scheme">The scheme.</param>
	/// <param name="appendVersion">if set to <c>true</c> appends a querystring version to the generated URL.</param>
	/// <param name="versionParameterName">Name of the version parameter.</param>
	/// <param name="mapFromContentRoot">if set to <c>true</c> maps the path from the content root..</param>
	/// <param name="pathBaseOverride">An optional base path used to override the path determined from the current HTTP Context.</param>
	/// <param name="hostOverride">An optional hostname used to override the hostname determined from the current HTTP Context.</param>
	/// <returns>The mapped path.</returns>
	string MapWebPath(string virtualPath, bool toAbsoluteUrl = false, string scheme = "https", bool appendVersion = false, string versionParameterName = "v", bool mapFromContentRoot = true, string? pathBaseOverride = null, string? hostOverride = null);

	/// <summary>
	/// Gets the string content of the file at the specified virtual path.
	/// </summary>
	/// <param name="virtualPath">The virtual path.</param>
	/// <param name="fromContentRoot">Determines whether to map the virtual path from the web (only applicable for ASP.NET Core) or the content root.</param>
	/// <param name="cache">Specifies if the content should be cached.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The file content.</returns>
	/// <returns></returns>
	Task<string?> GetFileContentAsync(string virtualPath, bool fromContentRoot, bool cache = true, CancellationToken cancellationToken = default);
}
