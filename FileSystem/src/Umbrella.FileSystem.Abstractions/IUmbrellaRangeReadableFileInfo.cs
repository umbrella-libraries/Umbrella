namespace Umbrella.FileSystem.Abstractions;

/// <summary>A file which supports reading a contiguous byte range.</summary>
public interface IUmbrellaRangeReadableFileInfo : IUmbrellaFileInfo
{

	/// <summary>Opens an authorized, caller-owned stream containing exactly the requested range.</summary>
	/// <param name="offset">The zero-based offset in the file.</param>
	/// <param name="length">The positive number of bytes to read.</param>
	/// <param name="bufferSizeOverride">An optional positive buffer size.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A stream whose disposal releases the underlying download or file.</returns>
	/// <remarks>The range must fit within <see cref="IUmbrellaFileInfo.Length"/>. Premature EOF is an error.</remarks>
	Task<Stream> ReadRangeAsStreamAsync(long offset, long length, int? bufferSizeOverride = null, CancellationToken cancellationToken = default);
}
