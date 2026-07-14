using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Umbrella.Testing.AspNetCore.Authentication;

namespace Umbrella.Testing.AspNetCore.Test;

public sealed class AuthenticationServiceCollectionExtensionsTests
{
	[Fact]
	public void ReplaceAuthenticationSchemeHandlerReplacesSchemeAndDefaults()
	{
		const string authenticationScheme = "Application";
		var services = new ServiceCollection();

		_ = services.AddOptions<AuthenticationOptions>().Configure(options =>
		{
			options.DefaultSignInScheme = "External";
			options.DefaultSignOutScheme = "External";
			options.AddScheme<OriginalAuthenticationHandler>(authenticationScheme, displayName: null);
		});

		_ = services.ReplaceAuthenticationSchemeHandler<ReplacementAuthenticationHandler>(
			authenticationScheme,
			configureSignInAndSignOut: true);

		using ServiceProvider serviceProvider = services.BuildServiceProvider();
		AuthenticationOptions options = serviceProvider.GetRequiredService<IOptions<AuthenticationOptions>>().Value;

		Assert.Equal(authenticationScheme, options.DefaultAuthenticateScheme);
		Assert.Equal(authenticationScheme, options.DefaultChallengeScheme);
		Assert.Equal(authenticationScheme, options.DefaultForbidScheme);
		Assert.Equal(authenticationScheme, options.DefaultScheme);
		Assert.Equal(authenticationScheme, options.DefaultSignInScheme);
		Assert.Equal(authenticationScheme, options.DefaultSignOutScheme);
		Assert.Equal(typeof(ReplacementAuthenticationHandler), options.SchemeMap[authenticationScheme].HandlerType);
		Assert.Contains(services, x => x.ServiceType == typeof(ReplacementAuthenticationHandler));
	}

	[Fact]
	public void ReplaceAuthenticationSchemeHandlerRejectsEmptyScheme()
	{
		var services = new ServiceCollection();

		_ = Assert.Throws<ArgumentException>(() => services.ReplaceAuthenticationSchemeHandler<ReplacementAuthenticationHandler>(" "));
	}

	[Fact]
	public void ReplaceAuthenticationSchemeHandlerAddsMissingSchemeWithoutChangingSignInOrSignOutDefaults()
	{
		const string authenticationScheme = "Application";
		var services = new ServiceCollection();

		_ = services.AddOptions<AuthenticationOptions>().Configure(options =>
		{
			options.DefaultSignInScheme = "External";
			options.DefaultSignOutScheme = "External";
		});

		_ = services.ReplaceAuthenticationSchemeHandler<ReplacementAuthenticationHandler>(authenticationScheme);

		using ServiceProvider serviceProvider = services.BuildServiceProvider();
		AuthenticationOptions options = serviceProvider.GetRequiredService<IOptions<AuthenticationOptions>>().Value;

		Assert.Equal("External", options.DefaultSignInScheme);
		Assert.Equal("External", options.DefaultSignOutScheme);
		Assert.Equal(typeof(ReplacementAuthenticationHandler), options.SchemeMap[authenticationScheme].HandlerType);
	}

	private abstract class AuthenticationHandlerStub : IAuthenticationHandler
	{
		public Task InitializeAsync(AuthenticationScheme scheme, HttpContext context) => Task.CompletedTask;

		public Task<AuthenticateResult> AuthenticateAsync() => Task.FromResult(AuthenticateResult.NoResult());

		public Task ChallengeAsync(AuthenticationProperties? properties) => Task.CompletedTask;

		public Task ForbidAsync(AuthenticationProperties? properties) => Task.CompletedTask;
	}

	private sealed class OriginalAuthenticationHandler : AuthenticationHandlerStub;

	private sealed class ReplacementAuthenticationHandler : AuthenticationHandlerStub;
}
