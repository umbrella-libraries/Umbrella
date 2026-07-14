using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace Umbrella.Testing.AspNetCore.Authentication;

/// <summary>
/// Extension methods for replacing application authentication schemes in integration tests.
/// </summary>
public static class AuthenticationServiceCollectionExtensions
{
	/// <summary>
	/// Replaces the handler for an application authentication scheme and makes that scheme the default for
	/// authentication, challenge, and forbid operations.
	/// </summary>
	/// <typeparam name="TAuthenticationHandler">The test authentication handler type.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="authenticationScheme">The application authentication scheme to replace.</param>
	/// <param name="configureSignInAndSignOut">
	/// <see langword="true"/> to also use the replacement scheme for sign-in and sign-out operations.
	/// </param>
	/// <returns>The service collection.</returns>
	/// <remarks>
	/// This method does not remove scheme-specific post-configuration. Consumers should remove interfering cookie
	/// post-configuration explicitly and only when the application requires it.
	/// </remarks>
	public static IServiceCollection ReplaceAuthenticationSchemeHandler<TAuthenticationHandler>(
		this IServiceCollection services,
		string authenticationScheme,
		bool configureSignInAndSignOut = false)
		where TAuthenticationHandler : class, IAuthenticationHandler
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(authenticationScheme);

		_ = services.AddTransient<TAuthenticationHandler>();
		_ = services.PostConfigure<AuthenticationOptions>(options =>
		{
			options.DefaultAuthenticateScheme = authenticationScheme;
			options.DefaultChallengeScheme = authenticationScheme;
			options.DefaultForbidScheme = authenticationScheme;
			options.DefaultScheme = authenticationScheme;

			if (configureSignInAndSignOut)
			{
				options.DefaultSignInScheme = authenticationScheme;
				options.DefaultSignOutScheme = authenticationScheme;
			}

			if (options.SchemeMap.TryGetValue(authenticationScheme, out AuthenticationSchemeBuilder? scheme))
				scheme.HandlerType = typeof(TAuthenticationHandler);
			else
				options.AddScheme<TAuthenticationHandler>(authenticationScheme, displayName: null);
		});

		return services;
	}
}
