


using CommunityToolkit.Diagnostics;
using Umbrella.AspNetCore.WebUtilities.Security.Policies;
using Umbrella.Utilities.Security.Policies;

#pragma warning disable IDE0130
namespace Microsoft.AspNetCore.Authorization;

/// <summary>
/// Extension methods for <see cref="AuthorizationOptions"/> instances.
/// </summary>
public static class AuthorizationOptionsExtensions
{
	/// <summary>
	/// Adds the core policies.
	/// </summary>
	/// <param name="options">The options.</param>
	public static void AddCorePolicies(this AuthorizationOptions options)
	{
		Guard.IsNotNull(options);

		options.AddPolicy(CorePolicyNames.Create, x => x.Requirements.Add(CoreItemOperations.Create));
		options.AddPolicy(CorePolicyNames.Read, x => x.Requirements.Add(CoreItemOperations.Read));
		options.AddPolicy(CorePolicyNames.Update, x => x.Requirements.Add(CoreItemOperations.Update));
		options.AddPolicy(CorePolicyNames.Delete, x => x.Requirements.Add(CoreItemOperations.Delete));
	}
}