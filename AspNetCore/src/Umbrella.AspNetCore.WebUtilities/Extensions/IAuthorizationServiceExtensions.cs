using System.Security.Claims;

#pragma warning disable IDE0130
namespace Microsoft.AspNetCore.Authorization;

/// <summary>
/// Extension methods for the <see cref="IAuthorizationService" /> type.
/// </summary>
public static class IAuthorizationServiceExtensions
{
	/// <summary>
	/// Checks if the specified <paramref name="user"/> is permitted to access all specified <paramref name="resources"/> using the provided <paramref name="policyName"/>.
	/// </summary>
	/// <typeparam name="TResource">The type of the resource being authorized for access by the specified user.</typeparam>
	/// <param name="authorizationService">The authorization service.</param>
	/// <param name="user">The user.</param>
	/// <param name="resources">The resources.</param>
	/// <param name="policyName">Name of the policy.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>An awaitable <see cref="Task"/> containing the result.</returns>
	public static async Task<bool> AuthorizeAllAsync<TResource>(this IAuthorizationService authorizationService, ClaimsPrincipal user, IEnumerable<TResource> resources, string policyName, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		ArgumentNullException.ThrowIfNull(authorizationService);
		ArgumentNullException.ThrowIfNull(resources);

		foreach (var item in resources)
		{
			var result = await authorizationService.AuthorizeAsync(user, item, policyName);

			if (!result.Succeeded)
				return false;
		}

		return true;
	}
}
