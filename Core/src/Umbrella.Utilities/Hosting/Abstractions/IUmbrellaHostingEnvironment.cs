namespace Umbrella.Utilities.Hosting.Abstractions;

/// <summary>
/// An abstraction over the current hosting environment.
/// </summary>
public interface IUmbrellaHostingEnvironment
{
	/// <summary>
	/// Maps the specified virtual path to an absolute path.
	/// </summary>
	/// <param name="virtualPath">The virtual path.</param>
	/// <returns>The mapped path.</returns>
	string? MapPath(string virtualPath);

	/// <summary>
	/// Gets the string content of the file at the specified virtual path.
	/// </summary>
	/// <param name="virtualPath">The virtual path.</param>
	/// <param name="cache">Specifies if the content should be cached.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The file content or null if it doesn't exist.</returns>
	Task<string?> GetFileContentAsync(string virtualPath, bool cache = true, CancellationToken cancellationToken = default);
}
