using System.Collections.Concurrent;
using System.Net.Http;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Extensions.Http;
using Polly.Retry;
using Polly.Timeout;

namespace Umbrella.Utilities.Http;

/// <summary>
/// A collection of default Http Policies using Polly.
/// </summary>
public static class HttpPolicies
{
	private static readonly ConcurrentDictionary<(int, int), AsyncRetryPolicy<HttpResponseMessage>> _policyDictionary = new();

	/// <summary>
	/// Creates the error and timeout policy.
	/// </summary>
	/// <param name="firstRetryDelaySeconds">The first retry delay in seconds.</param>
	/// <param name="retryCount">The retry count.</param>
	/// <returns>The policy.</returns>
	public static AsyncRetryPolicy<HttpResponseMessage> CreateErrorAndTimeoutPolicy(int firstRetryDelaySeconds = 2, int retryCount = 3)
		=> _policyDictionary.GetOrAdd((firstRetryDelaySeconds, retryCount), HttpPolicyExtensions.HandleTransientHttpError()
			.Or<TimeoutRejectedException>()
			.WaitAndRetryAsync(Backoff.DecorrelatedJitterBackoffV2(TimeSpan.FromSeconds(firstRetryDelaySeconds), retryCount)));
}