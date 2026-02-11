
namespace Umbrella.Utilities.Email;

/// <summary>
/// An email attachment for use with the <see cref="EmailSender" /> class.
/// </summary>
public class EmailAttachment : IDisposable
{
	private const string DefaultContentType = "application/octet-stream";
	private bool _disposedValue;

	/// <summary>
	/// The name of the file.
	/// </summary>
	public string FileName { get; }

	/// <summary>
	/// The content type of the file.
	/// </summary>
	public string ContentType { get; }

	/// <summary>
	/// The content of the file.
	/// </summary>
	public Stream Content { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="EmailAttachment"/> class.
	/// </summary>
	/// <param name="fileName">The file name.</param>
	/// <param name="content">The content.</param>
	/// <param name="contentType">The content type.</param>
	public EmailAttachment(string fileName, Stream content, string? contentType = null)
	{
		FileName = fileName;
		Content = content;
		ContentType = contentType ?? DefaultContentType;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="EmailAttachment"/> class.
	/// </summary>
	/// <param name="fileName">The file name.</param>
	/// <param name="content">The content.</param>
	/// <param name="contentType">The content type.</param>
	public EmailAttachment(string fileName, byte[] content, string? contentType = null)
	{
		FileName = fileName;
		Content = new MemoryStream(content);
		ContentType = contentType ?? DefaultContentType;
	}

	/// <summary>
	/// Disposes of the attachment.
	/// </summary>
	/// <param name="disposing">Specifies whether the object is being disposed.</param>
	protected virtual void Dispose(bool disposing)
	{
		if (!_disposedValue)
		{
			if (disposing)
			{
				Content?.Dispose();
			}

			_disposedValue = true;
		}
	}

	/// <inheritdoc />
	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
}