


using System.Net.Http;

namespace Umbrella.Utilities.Http;

/// <summary>
/// Contains additional HttpMethods that are missing from the <see cref="HttpMethod"/> type.
/// </summary>
public static class HttpMethodExtras
{
	/// <summary>
	/// Gets the patch HTTP method.
	/// </summary>
	public static HttpMethod Patch { get; } = new HttpMethod("PATCH");
}