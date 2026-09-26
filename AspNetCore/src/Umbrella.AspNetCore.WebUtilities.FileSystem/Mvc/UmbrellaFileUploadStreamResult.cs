using Microsoft.AspNetCore.Mvc;

namespace Umbrella.AspNetCore.WebUtilities.FileSystem.Mvc;

/// <summary>Either a readable upload stream with explicit ownership, or an HTTP rejection.</summary>
public sealed class UmbrellaFileUploadStreamResult
{
	private UmbrellaFileUploadStreamResult(Stream? stream, bool ownsStream, IActionResult? rejection)
	{
		Stream = stream;
		OwnsStream = ownsStream;
		Rejection = rejection;
	}

	/// <summary>Gets the stream for a successful preparation.</summary>
	public Stream? Stream { get; }
	/// <summary>Gets whether the controller must dispose the prepared stream.</summary>
	public bool OwnsStream { get; }
	/// <summary>Gets the HTTP response for rejected preparation.</summary>
	public IActionResult? Rejection { get; }

	/// <summary>Prepares a readable stream. Ownership must be explicitly selected by the caller.</summary>
	/// <param name="stream">The stream to upload.</param>
	/// <param name="ownsStream">True to transfer disposal responsibility to the controller.</param>
	/// <returns>A successful preparation result.</returns>
	public static UmbrellaFileUploadStreamResult Success(Stream stream, bool ownsStream)
	{
		ArgumentNullException.ThrowIfNull(stream);
		if (!stream.CanRead)
			throw new ArgumentException("The upload stream must be readable.", nameof(stream));
		return new(stream, ownsStream, null);
	}

	/// <summary>Rejects the upload without transferring any stream ownership.</summary>
	/// <param name="rejection">The response to return.</param>
	/// <returns>A rejected preparation result.</returns>
	public static UmbrellaFileUploadStreamResult Reject(IActionResult rejection)
	{
		ArgumentNullException.ThrowIfNull(rejection);
		return new(null, false, rejection);
	}
}
