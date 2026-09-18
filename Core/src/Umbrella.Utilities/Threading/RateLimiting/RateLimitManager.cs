using Umbrella.Utilities.Dating.Abstractions;
using Umbrella.Utilities.Threading.RateLimiting.Abstractions;
using Umbrella.Utilities.Threading.RateLimiting.Exceptions;

namespace Umbrella.Utilities.Threading.RateLimiting;

/// <summary>
/// The default implementation of <see cref="IRateLimitManager{TPartitionKey, TResourceKey}"/>.
/// </summary>
public sealed class RateLimitManager<TPartitionKey, TResourceKey> : IRateLimitManager<TPartitionKey, TResourceKey>
	where TPartitionKey : notnull
	where TResourceKey : notnull
{
	private readonly IRateLimitStore<TPartitionKey, TResourceKey> _store;
	private readonly IDateTimeProvider _dateTimeProvider;

	/// <summary>
	/// Initializes a new instance of the <see cref="RateLimitManager{TPartitionKey, TResourceKey}"/> class.
	/// </summary>
	public RateLimitManager(IRateLimitStore<TPartitionKey, TResourceKey> store, IDateTimeProvider dateTimeProvider)
	{
		_store = store ?? throw new ArgumentNullException(nameof(store));
		_dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
	}

	/// <inheritdoc />
	public async Task<RateLimitAcquisitionResult> TryAcquireAsync(
		TPartitionKey partitionKey,
		TResourceKey resourceKey,
		RateLimitPolicy policy,
		CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		if (partitionKey is null)
			throw new ArgumentNullException(nameof(partitionKey));

		if (resourceKey is null)
			throw new ArgumentNullException(nameof(resourceKey));

		if (policy is null)
			throw new ArgumentNullException(nameof(policy));

		try
		{
			return await _store.TryAcquireAsync(partitionKey, resourceKey, policy, _dateTimeProvider.UtcNow, cancellationToken).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (RateLimitUnavailableException)
		{
			throw;
		}
		catch (Exception exc)
		{
			throw new RateLimitUnavailableException("The rate limit service is temporarily unavailable.", exc);
		}
	}
}
