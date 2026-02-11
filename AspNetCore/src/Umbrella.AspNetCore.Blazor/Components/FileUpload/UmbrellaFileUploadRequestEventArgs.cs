
using System.Runtime.InteropServices;

namespace Umbrella.AspNetCore.Blazor.Components.FileUpload;

/// <summary>
/// The event arguments for file upload events raised by the <see cref="UmbrellaFileUpload"/> component.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct UmbrellaFileUploadRequestEventArgs
{
	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaFileUploadRequestEventArgs"/> struct.
	/// </summary>
	/// <param name="content">The content.</param>
	/// <param name="fileName">Name of the file.</param>
	/// <param name="type">The type.</param>
	/// <param name="uploadCancellationToken">The upload cancellation token.</param>
	public UmbrellaFileUploadRequestEventArgs(Stream content, string fileName, string type, CancellationToken uploadCancellationToken)
	{
		Content = content;
		FileName = fileName;
		Type = type;
		UploadCancellationToken = uploadCancellationToken;
	}

	/// <summary>
	/// Gets the content.
	/// </summary>
	public Stream Content { get; }

	/// <summary>
	/// Gets the name of the file.
	/// </summary>
	public string FileName { get; }

	/// <summary>
	/// Gets the MIME type of the file.
	/// </summary>
	public string Type { get; }

	/// <summary>
	/// Gets the upload cancellation token.
	/// </summary>
	public CancellationToken UploadCancellationToken { get; }
}