


using CommunityToolkit.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace Umbrella.AspNetCore.Shared.Extensions;

/// <summary>
/// Extension methods for the <see cref="IAuthorizationService" /> type.
/// </summary>
public static class IAuthorizationServiceExtensions
{
	/// <summary>
	/// Checks if the specified <paramref name= "user" /> has the necessary specified <paramref name="roles"/> and meets the specified <paramref name="policyName"/>.
	/// </summary>
	/// <param name="authorizationService">The authorization service.</param>
	/// <param name="user">The user.</param>
	/// <param name="roles">The roles.</param>
	/// <param name="policyName">Name of the policy.</param>
	/// <returns>An awaitable <see cref="Task"/> containing the result.</returns>
	public static async Task<bool> AuthorizeRolesAndPolicyAsync(this IAuthorizationService authorizationService, ClaimsPrincipal user, string? roles, string? policyName)
	{
		Guard.IsNotNull(user);

		if (string.IsNullOrEmpty(roles) && string.IsNullOrEmpty(policyName))
		{
			// No roles or policy specified so just return true.
			return true;
		}
		else
		{
			// Start by assuming both types of checks are authorized.
			// If roles and/or a policy have been provided they can be invalidated.
			bool rolesAuthorized = true;
			bool policyAuthorized = true;

			if (!string.IsNullOrEmpty(roles))
			{
				string[] lstRole = roles.Split(',', StringSplitOptions.RemoveEmptyEntries);

				if (lstRole.Length > 0)
					rolesAuthorized = lstRole.Any(user.IsInRole);
			}

			if (!string.IsNullOrEmpty(policyName))
			{
				AuthorizationResult authResult = await authorizationService.AuthorizeAsync(user, policyName);
				policyAuthorized = authResult.Succeeded;
			}

			return rolesAuthorized && policyAuthorized;
		}
	}
}