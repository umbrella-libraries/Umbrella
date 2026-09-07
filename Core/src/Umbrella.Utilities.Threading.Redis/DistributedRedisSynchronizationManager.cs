using CommunityToolkit.HighPerformance.Buffers;
using Medallion.Threading.Redis;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Umbrella.Utilities.Exceptions;
using Umbrella.Utilities.Threading.Abstractions;
using Umbrella.Utilities.Threading.Redis.Options;

namespace Umbrella.Utilities.Threading.Redis;
/// <summary>
/// An implementation of the <see cref="ISynchronizationManager"/> interface that uses Redis cache internally
/// to perform synchronization.
/// </summary>
/// <seealso cref="ISynchronizationManager" />
public sealed class DistributedRedisSynchronizationManager : ISynchronizationManager, IDisposable, IAsyncDisposable
{
	private readonly ILogger _logger;
	private readonly DistributedRedisSynchronizationManagerOptions _options;
	private readonly StringPool _stringPool = new();
	private readonly SemaphoreSlim _connectionLock = new(1, 1);
	private ConnectionMultiplexer? _connection;

	/// <summary>
	/// Initializes a new instance of the <see cref="DistributedRedisSynchronizationManager"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="options">The options.</param>
	public DistributedRedisSynchronizationManager(
		ILogger<DistributedRedisSynchronizationManager> logger,
		DistributedRedisSynchronizationManagerOptions options)
	{
		_logger = logger;
		_options = options;
	}

	/// <inheritdoc />
	public async ValueTask<ISynchronizationRoot> GetSynchronizationRootAndWaitAsync(Type type, string key, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (type is null)
			throw new ArgumentNullException(nameof(type));

		try
		{
			string qualifiedKey = _stringPool.GetOrAdd($"{type.FullName}:{key}");

			ConnectionMultiplexer connection = await GetConnectionAsync(cancellationToken);

			var @lock = new RedisDistributedLock(qualifiedKey, connection.GetDatabase());

			var syncRoot = new DistributedRedisSynchronizationRoot(@lock);

			_ = await syncRoot.WaitAsync(cancellationToken);

			return syncRoot;
		}
		catch (Exception exc) when (exc is not OperationCanceledException && _logger.WriteError(exc, new { TypeName = type.FullName, key }))
		{
			throw new UmbrellaException("There has been a problem getting the synchronization root.", exc);
		}
	}

	/// <inheritdoc />
	public ValueTask<ISynchronizationRoot> GetSynchronizationRootAndWaitAsync<T>(string key, CancellationToken cancellationToken = default)
		=> GetSynchronizationRootAndWaitAsync(typeof(T), key, cancellationToken);

	/// <inheritdoc />
	public void Dispose()
	{
		_connection?.Dispose();
		_connectionLock.Dispose();

		GC.SuppressFinalize(this);
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		if (_connection is not null)
			await _connection.DisposeAsync();

		_connectionLock.Dispose();

		GC.SuppressFinalize(this);
	}

	private async ValueTask<ConnectionMultiplexer> GetConnectionAsync(CancellationToken cancellationToken)
	{
		ConnectionMultiplexer? connection = Volatile.Read(ref _connection);

		if (connection is not null)
			return connection;

		await _connectionLock.WaitAsync(cancellationToken);

		try
		{
			connection = Volatile.Read(ref _connection);

			if (connection is null)
			{
				connection = await ConnectionMultiplexer.ConnectAsync(_options.ConnectionString);
				Volatile.Write(ref _connection, connection);
			}

			return connection;
		}
		finally
		{
			_ = _connectionLock.Release();
		}
	}
}
