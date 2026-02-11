using System.Collections.Concurrent;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Logging;
using Umbrella.Utilities.Exceptions;
using Umbrella.Utilities.Threading.Abstractions;

namespace Umbrella.Utilities.Threading;

/// <summary>
/// An implementation of the <see cref="ISynchronizationManager"/> interface that uses a <see cref="SemaphoreSlim" /> internally
/// to perform synchronization.
/// </summary>
/// <seealso cref="ISynchronizationManager" />
public class MemorySynchronizationManager : ISynchronizationManager
{
	private readonly ConcurrentDictionary<string, SemaphoreSlim> _items = new();
	private readonly ILogger<MemorySynchronizationManager> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="MemorySynchronizationManager"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	public MemorySynchronizationManager(ILogger<MemorySynchronizationManager> logger)
	{
		_logger = logger;
	}

	/// <inheritdoc />
	public async ValueTask<ISynchronizationRoot> GetSynchronizationRootAndWaitAsync(Type type, string key, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(type);
		Guard.IsNotNullOrEmpty(key);

		try
		{
			var semaphoreSlim = _items.GetOrAdd(string.Intern($"{type.FullName}:{key}"), x => new SemaphoreSlim(1, 1));

			var syncRoot = new MemorySynchronizationRoot(semaphoreSlim);

			_ = await syncRoot.WaitAsync(cancellationToken).ConfigureAwait(false);

			return syncRoot;
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { TypeName = type.FullName, key }))
		{
			throw new UmbrellaException("There has been a problem getting the synchronization root.", exc);
		}
	}

	/// <inheritdoc />
	public ValueTask<ISynchronizationRoot> GetSynchronizationRootAndWaitAsync<T>(string key, CancellationToken cancellationToken = default)
		=> GetSynchronizationRootAndWaitAsync(typeof(T), key, cancellationToken);
}