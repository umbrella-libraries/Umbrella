using System.Security.Claims;

namespace Umbrella.AppFramework.Security.Abstractions;

/// <summary>
/// A helper used to perform common auth actions.
/// </summary>
/// <remarks>
/// This is registered with DI as a scoped service due to internal dependencies which are also scoped.
/// However, care must be taken when using this service in conjunction with HttpClientFactory and DelegatingHandlers
/// because services resolved there use their own scope which means working with a different instance.
/// </remarks>
public interface IAppAuthHelper
{
	/// <summary>
	/// Invoked when the authentication state changes.
	/// </summary>
	event Func<ClaimsPrincipal, Task> OnAuthenticationStateChanged;

	/// <summary>
	/// Set the current claims principal using the specified authentication token.
	/// </summary>
	/// <param name="token">A token used to construct the claims principal.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The <see cref="ClaimsPrincipal"/>.</returns>
	ValueTask<ClaimsPrincipal> SetCurrentClaimsPrincipalAsync(string token, CancellationToken cancellationToken = default);

	/// <summary>
	/// Logs out the current user from the app locally, i.e. without telling the server.
	/// </summary>
	/// <param name="executeDefaultPostLogoutAction">if set to <c>true</c> executes any additional registered post logout actions.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>An task used to await completion of the operation.</returns>
	ValueTask LocalLogoutAsync(bool executeDefaultPostLogoutAction = true, CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets the current <see cref="ClaimsPrincipal"/> asynchronously based on ambient state.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The <see cref="ClaimsPrincipal"/>.</returns>
	ValueTask<ClaimsPrincipal> GetCurrentClaimsPrincipalAsync(CancellationToken cancellationToken = default);
}