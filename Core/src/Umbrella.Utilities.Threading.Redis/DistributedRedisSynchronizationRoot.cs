using Medallion.Threading.Redis;
using Umbrella.Utilities.Threading.Abstractions;

namespace Umbrella.Utilities.Threading.Redis;

/// <summary>
/// An implementation of <see cref="ISynchronizationRoot"/> that uses a <see cref="RedisDistributedLock"/> internally to perform synchronization.
/// </summary>
/// <seealso cref="ISynchronizationRoot" />
public sealed class DistributedRedisSynchronizationRoot : ISynchronizationRoot
{
	private readonly RedisDistributedLock _lock;
	private RedisDistributedLockHandle? _handle;

	internal DistributedRedisSynchronizationRoot(RedisDistributedLock @lock)
	{
		_lock = @lock;
	}

	/// <summary>
	/// The finalizer for this instance.
	/// </summary>
	~DistributedRedisSynchronizationRoot()
	{
		_handle?.Dispose();
	}

	internal async ValueTask<ISynchronizationRoot> WaitAsync(CancellationToken cancellationToken = default)
	{
		_handle = await _lock.AcquireAsync(cancellationToken: cancellationToken);

		return this;
	}

	/// <inheritdoc />
	public void Dispose()
	{
		_handle?.Dispose();
		_handle = null;

		GC.SuppressFinalize(this);
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		if (_handle is not null)
		{
			await _handle.DisposeAsync();
			_handle = null;
		}

		GC.SuppressFinalize(this);
	}
}
