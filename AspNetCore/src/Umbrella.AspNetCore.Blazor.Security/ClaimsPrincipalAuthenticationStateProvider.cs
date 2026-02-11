using System.Security.Claims;
using CommunityToolkit.Diagnostics;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using Umbrella.AppFramework.Security.Abstractions;
using Umbrella.AppFramework.Shared.Security.Extensions;
using Umbrella.AspNetCore.Blazor.Security.Abstractions;
using Umbrella.AspNetCore.Blazor.Security.Exceptions;
using Umbrella.AspNetCore.Blazor.Security.Options;
using Umbrella.Utilities.Dating.Abstractions;

namespace Umbrella.AspNetCore.Blazor.Security;

/// <summary>
/// An authentication state provider used to mark a <see cref="ClaimsPrincipal"/> as being authenticated as the current user, or mark any existing one
/// as logged out.
/// </summary>
/// <seealso cref="AuthenticationStateProvider" />
/// <seealso cref="IClaimsPrincipalAuthenticationStateProvider" />
public class ClaimsPrincipalAuthenticationStateProvider : AuthenticationStateProvider, IClaimsPrincipalAuthenticationStateProvider
{
	private readonly ILogger _logger;
	private readonly IAppAuthHelper _authHelper;
	private readonly IDateTimeProvider _dateTimeProvider;
	private readonly ClaimsPrincipalAuthenticationStateProviderOptions _options;

	/// <inheritdoc />
	public event EventHandler? AuthenticatedStateHasChanged;

	/// <summary>
	/// Initializes a new instance of the <see cref="ClaimsPrincipalAuthenticationStateProvider"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="authHelper">The authentication helper.</param>
	/// <param name="dateTimeProvider">The date time provider.</param>
	/// <param name="options">The options.</param>
	public ClaimsPrincipalAuthenticationStateProvider(
		ILogger<ClaimsPrincipalAuthenticationStateProvider> logger,
		IAppAuthHelper authHelper,
		IDateTimeProvider dateTimeProvider,
		ClaimsPrincipalAuthenticationStateProviderOptions options)
	{
		_logger = logger;
		_authHelper = authHelper;
		_dateTimeProvider = dateTimeProvider;
		_options = options;
		_authHelper.OnAuthenticationStateChanged += MarkUserAsAuthenticatedAsync;
	}

	/// <inheritdoc />
	public override async Task<AuthenticationState> GetAuthenticationStateAsync()
	{
		try
		{
			ClaimsPrincipal principal = await _authHelper.GetCurrentClaimsPrincipalAsync();

			// Check the refresh token for expiration
			DateTime? refreshTokenExpiration = principal.GetRefreshTokenExpiration();

			if (refreshTokenExpiration > _dateTimeProvider.UtcNow)
			{
				if (_options.OnSetAuthenticatedUserContext is not null)
					await _options.OnSetAuthenticatedUserContext(principal);

				return new AuthenticationState(principal);
			}
			else
			{
				if (_options.OnClearAuthenticatedUserContext is not null)
					await _options.OnClearAuthenticatedUserContext();

				return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
			}
		}
		catch (Exception exc) when (_logger.WriteError(exc))
		{
			throw new UmbrellaBlazorSecurityException("There has been a problem getting the authentication state.", exc);
		}
	}

	/// <inheritdoc />
	public async Task MarkUserAsAuthenticatedAsync(ClaimsPrincipal principal)
	{
		Guard.IsNotNull(principal);

		try
		{
			var authState = Task.FromResult(new AuthenticationState(principal));
			NotifyAuthenticationStateChanged(authState);
			AuthenticatedStateHasChanged?.Invoke(this, EventArgs.Empty);

			if (_options.OnSetAuthenticatedUserContext is not null)
				await _options.OnSetAuthenticatedUserContext(principal);
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { principal.Identity?.Name }))
		{
			throw new UmbrellaBlazorSecurityException("There has been a problem marking the user as authenticated.", exc);
		}
	}

	/// <inheritdoc />
	public async Task MarkUserAsLoggedOutAsync()
	{
		try
		{
			var authState = Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));
			NotifyAuthenticationStateChanged(authState);
			AuthenticatedStateHasChanged?.Invoke(this, EventArgs.Empty);

			if (_options.OnClearAuthenticatedUserContext is not null)
				await _options.OnClearAuthenticatedUserContext();
		}
		catch (Exception exc) when (_logger.WriteError(exc))
		{
			throw new UmbrellaBlazorSecurityException("There has been a problem marking the user as logged out.", exc);
		}
	}
}