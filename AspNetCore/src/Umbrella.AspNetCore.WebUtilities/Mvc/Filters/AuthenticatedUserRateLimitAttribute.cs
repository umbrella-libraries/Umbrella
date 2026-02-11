using System.Collections.Concurrent;
using System.Security.Principal;
using CommunityToolkit.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Umbrella.AspNetCore.WebUtilities.Mvc.Filters;

/// <summary>
/// Allows rate limiting to be applied to the target controller or action method, scoped to the current
/// authenticated user using their <see cref="IIdentity.Name"/>. Rate limiting will not be applied to anonymous users.
/// </summary>
/// <seealso cref="Attribute" />
/// <seealso cref="IResourceFilter" />
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class AuthenticatedUserRateLimitAttribute : Attribute, IResourceFilter
{
	private readonly ConcurrentDictionary<string, DateTime> _rateLimitTrackingDictionary = new();

	/// <summary>
	/// Gets or sets the limit per minute per authenticated user.
	/// </summary>
	public double LimitPerMinute { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="AuthenticatedUserRateLimitAttribute"/> class.
	/// </summary>
	/// <param name="limitPerMinute">The limit per minute.</param>
	public AuthenticatedUserRateLimitAttribute(double limitPerMinute)
	{
		LimitPerMinute = limitPerMinute;
	}

	/// <inheritdoc />
	public void OnResourceExecuted(ResourceExecutedContext context)
	{
	}

	/// <inheritdoc />
	public void OnResourceExecuting(ResourceExecutingContext context)
	{
		Guard.IsNotNull(context);

		if (context.HttpContext.User.Identity?.IsAuthenticated is true)
		{
			string? userName = context.HttpContext.User.Identity.Name;

			if (userName is null)
				return;

			DateTime utcNow = DateTime.UtcNow;

			if (_rateLimitTrackingDictionary.TryGetValue(userName, out DateTime nextRetryAllowed) && utcNow < nextRetryAllowed)
			{
				int secondsRemaining = (int)Math.Ceiling(nextRetryAllowed.Subtract(utcNow).TotalSeconds);
				string secondsRemainingDescriptor = secondsRemaining is 1 ? "second" : "seconds";

				string message = $"Please wait for another {secondsRemaining} {secondsRemainingDescriptor} before retrying.";

				context.Result = new ObjectResult(message)
				{
					StatusCode = 429
				};
			}
			else
			{
				_rateLimitTrackingDictionary[userName] = DateTime.UtcNow.AddSeconds(60 / LimitPerMinute);
			}
		}
	}
}