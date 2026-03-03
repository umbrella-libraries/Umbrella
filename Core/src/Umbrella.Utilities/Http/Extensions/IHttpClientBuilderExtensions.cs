using System.Net.Http;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Polly;

namespace Umbrella.Utilities.Http.Extensions;

/// <summary>
/// Extension methods for the <see cref="IHttpClientBuilder"/>.
/// </summary>
public static class IHttpClientBuilderExtensions
{
	/// <summary>
	/// Adds the default policy handlers.
	/// </summary>
	/// <remarks>
	/// When a transient HTTP error is encountered, or the <paramref name="timeout"/> is exceeded, there will be a delay of <paramref name="firstRetryDelaySeconds"/> before
	/// another attempt is made to retry the HTTP request. Subsequent errors will result in a longer delay using a jitter algorithm before retrying again.
	/// A maximum of <paramref name="retryCount"/> retries will be made at which point any exception that caused the request to fail will be raised to be dealt with at
	/// the call site.
	/// </remarks>
	/// <param name="builder">The builder.</param>
	/// <param name="timeout">The request timeout in seconds.</param>
	/// <param name="firstRetryDelaySeconds">The first retry delay in seconds.</param>
	/// <param name="retryCount">The retry count in case of failure.</param>
	/// <returns>The builder.</returns>
#if NET8_0_OR_GREATER
[Obsolete("Please use the new extension methods provided by Microsoft in the Microsoft.Extensions.Http.Resilience NuGet package.")]
#endif
	public static IHttpClientBuilder AddUmbrellaPolicyHandlers(this IHttpClientBuilder builder, int timeout = 5, int firstRetryDelaySeconds = 2, int retryCount = 3)
	{
		Guard.IsNotNull(builder);
		Guard.IsGreaterThan(timeout, 0);
		Guard.IsGreaterThan(firstRetryDelaySeconds, 0);
		Guard.IsGreaterThan(retryCount, 0);

		return builder.AddPolicyHandler(HttpPolicies.CreateErrorAndTimeoutPolicy(firstRetryDelaySeconds, retryCount)).AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(timeout));
	}
}