namespace Umbrella.FileSystem.Abstractions;

/// <summary>A read-only, forward-only view of a fixed number of bytes from the current position of a stream.</summary>
/// <remarks>Owns the source and optional response owner. Throws on premature EOF instead of silently truncating a range.</remarks>
public sealed class UmbrellaFileRangeStream : Stream
{
	private readonly Stream _source;
	private readonly IDisposable? _owner;
	private readonly long _length;
	private long _remaining;
	private bool _disposed;

	/// <summary>Creates a bounded view and takes ownership of the source and optional response.</summary>
	/// <param name="source">The source, already positioned at the range start.</param>
	/// <param name="length">The positive number of bytes in the range.</param>
	/// <param name="owner">An optional response object to dispose with the stream.</param>
	public UmbrellaFileRangeStream(Stream source, long length, IDisposable? owner = null)
	{
		_source = source ?? throw new ArgumentNullException(nameof(source));
		if (length <= 0)
			throw new ArgumentOutOfRangeException(nameof(length));
		_length = _remaining = length;
		_owner = owner;
	}

	/// <summary>Validates a range without overflowing when adding offset and length.</summary>
	/// <param name="fileLength">The full file length.</param>
	/// <param name="offset">The zero-based range offset.</param>
	/// <param name="length">The positive range length.</param>
	/// <param name="bufferSizeOverride">An optional positive buffer size.</param>
	public static void Validate(long fileLength, long offset, long length, int? bufferSizeOverride)
	{
		if (offset < 0 || offset >= fileLength)
			throw new ArgumentOutOfRangeException(nameof(offset));
		if (length <= 0 || length > fileLength - offset)
			throw new ArgumentOutOfRangeException(nameof(length));
		if (bufferSizeOverride <= 0)
			throw new ArgumentOutOfRangeException(nameof(bufferSizeOverride));
	}

	/// <inheritdoc />
	public override bool CanRead => !_disposed && _source.CanRead;

	/// <inheritdoc />
	public override bool CanSeek => false;

	/// <inheritdoc />
	public override bool CanWrite => false;

	/// <inheritdoc />
	public override long Length => _length;

	/// <inheritdoc />
	public override long Position { get => _length - _remaining; set => throw new NotSupportedException(); }

	/// <inheritdoc />
	public override int Read(byte[] buffer, int offset, int count)
	{
		ValidateRead(buffer, offset, count);
		if (_remaining == 0 || count == 0)
			return 0;
		return RecordRead(_source.Read(buffer, offset, (int)Math.Min(count, _remaining)));
	}

	/// <inheritdoc />
	public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
	{
		ValidateRead(buffer, offset, count);
		cancellationToken.ThrowIfCancellationRequested();
		if (_remaining == 0 || count == 0)
			return 0;
		return RecordRead(await _source.ReadAsync(buffer, offset, (int)Math.Min(count, _remaining), cancellationToken).ConfigureAwait(false));
	}

	private void ValidateRead(byte[] buffer, int offset, int count)
	{
		if (_disposed)
			throw new ObjectDisposedException(nameof(UmbrellaFileRangeStream));
		if (buffer is null)
			throw new ArgumentNullException(nameof(buffer));
		if (offset < 0 || offset > buffer.Length)
			throw new ArgumentOutOfRangeException(nameof(offset));
		if (count < 0 || count > buffer.Length - offset)
			throw new ArgumentOutOfRangeException(nameof(count));
	}

	private int RecordRead(int count)
	{
		if (count == 0)
			throw new EndOfStreamException("The file ended before the requested range was read.");
		_remaining -= count;
		return count;
	}

	/// <inheritdoc />
	protected override void Dispose(bool disposing)
	{
		if (disposing && !_disposed)
		{
			_disposed = true;
			try
			{
				_source.Dispose();
			}
			finally
			{
				_owner?.Dispose();
			}
		}

		base.Dispose(disposing);
	}

	/// <inheritdoc />
	public override void Flush() => throw new NotSupportedException();

	/// <inheritdoc />
	public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

	/// <inheritdoc />
	public override void SetLength(long value) => throw new NotSupportedException();

	/// <inheritdoc />
	public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
