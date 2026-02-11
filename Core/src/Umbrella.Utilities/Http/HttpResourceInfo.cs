
using CommunityToolkit.Diagnostics;

namespace Umbrella.Utilities.Http;

/// <summary>
/// Represents basic metadata of an HTTP resource.
/// </summary>
public class HttpResourceInfo
{
	/// <summary>
	/// Initializes a new instance of the <see cref="HttpResourceInfo"/> class.
	/// </summary>
	/// <param name="contentType">Type of the content.</param>
	/// <param name="contentLength">Length of the content.</param>
	/// <param name="lastModified">The last modified.</param>
	/// <param name="url">The URL.</param>
	public HttpResourceInfo(string contentType, long contentLength, DateTime? lastModified, string url)
	{
		Guard.IsNotNullOrWhiteSpace(contentType, nameof(contentType));
		Guard.IsNotNullOrWhiteSpace(url, nameof(url));

		ContentType = contentType;
		ContentLength = contentLength;
		LastModified = lastModified;
		Url = url;
	}

	/// <summary>
	/// Gets or sets the type of the content.
	/// </summary>
	public string ContentType { get; }

	/// <summary>
	/// Gets or sets the length of the content.
	/// </summary>
	public long ContentLength { get; }

	/// <summary>
	/// Gets or sets the last modified date.
	/// </summary>
	public DateTime? LastModified { get; }

	/// <summary>
	/// Gets or sets the URL.
	/// </summary>
	public string Url { get; }
}