


namespace Umbrella.AppFramework.Shared.Constants;

/// <summary>
/// Core HTTP Header names used when uploading files from client applications developed using the Umbrella.AppFramework.
/// </summary>
public static class FileUploadHeaderNames
{
	/// <summary>
	/// The header containing the name of the file being uploaded.
	/// </summary>
	public const string Name = "X-FileName";

	/// <summary>
	/// The header containing the content type of the file being uploaded.
	/// </summary>
	public const string ContentType = "X-FileContentType";

	/// <summary>
	/// The header containing the type of the file being uploaded.
	/// </summary>
	/// <remarks>
	/// The value should be a <see langword="string"/> that can be parsed to an <see langword="enum"/> on the server
	/// when being used with the <c>FileUploadController</c> from the <c>Umbrella.AspNetCore.WebUtilities</c> project,
	/// e.g. <c>Image</c>, <c>Document</c>, etc.
	/// </remarks>
	public const string UploadType = "X-FileUploadType";
}