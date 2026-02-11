


using System.Security.Claims;

namespace Umbrella.AspNetCore.Blazor.Security.Options;

/// <summary>
/// Options for use with the <see cref="ClaimsPrincipalAuthenticationStateProvider"/>.
/// </summary>
public class ClaimsPrincipalAuthenticationStateProviderOptions
{
	/// <summary>
	/// Gets or sets the callback that will be invoked when the authenticated user context is set.
	/// </summary>
	/// <remarks>
	/// This should be used to set the context on a logging provider, e.g. Application Insights, so that telemetry can be associated with the authenticated user.
	/// </remarks>
	public Func<ClaimsPrincipal, Task>? OnSetAuthenticatedUserContext { get; set; }

	/// <summary>
	/// Gets or sets the callback that will be invoked when the authenticated user context is cleared.
	/// </summary>
	/// <remarks>
	/// This should be used to clear the context on a logging provider, e.g. Application Insights, so that telemetry is no longer associated with a specific user.
	/// </remarks>
	public Func<Task>? OnClearAuthenticatedUserContext { get; set; }
}