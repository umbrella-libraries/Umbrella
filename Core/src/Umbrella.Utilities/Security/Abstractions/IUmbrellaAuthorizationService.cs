
using System.Security.Claims;

namespace Umbrella.Utilities.Security.Abstractions;

/// <summary>
/// Checks policy based permissions for a user
/// </summary>
public interface IUmbrellaAuthorizationService
{
	/// <summary>
	/// Checks if the specified <paramref name="user"/> is permitted to access all specified <paramref name="resources"/> using the provided <paramref name="policyName"/>.
	/// </summary>
	/// <typeparam name="TResource">The type of the resource being authorized for access by the specified user.</typeparam>
	/// <param name="user">The user.</param>
	/// <param name="resources">The resources.</param>
	/// <param name="policyName">Name of the policy.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>An awaitable <see cref="Task"/> containing the result.</returns>
	Task<bool> AuthorizeAllAsync<TResource>(ClaimsPrincipal user, IEnumerable<TResource> resources, string policyName, CancellationToken cancellationToken = default);

	/// <summary>
	/// Checks if a user meets a specific authorization policy
	/// </summary>
	/// <param name="user">The user to check the policy against.</param>
	/// <param name="resource">
	/// An optional resource the policy should be checked with.
	/// If a resource is not required for policy evaluation you may pass null as the value.
	/// </param>
	/// <param name="policyName">The name of the policy to check against a specific context.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>
	/// A flag indicating whether authorization has succeeded.
	/// Returns a flag indicating whether the user, and optional resource has fulfilled the policy.
	/// <value>true</value> when the policy has been fulfilled; otherwise <value>false</value>.
	/// </returns>
	/// <remarks>
	/// Resource is an optional parameter and may be null. Please ensure that you check it is not
	/// null before acting upon it.
	/// </remarks>
	Task<bool> AuthorizeAsync(ClaimsPrincipal user, object? resource, string policyName, CancellationToken cancellationToken = default);
}