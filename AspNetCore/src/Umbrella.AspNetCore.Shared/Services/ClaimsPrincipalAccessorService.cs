using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Umbrella.AspNetCore.Shared.Services.Abstractions;
using Umbrella.Utilities.Exceptions;

namespace Umbrella.AspNetCore.Shared.Services;

internal sealed class ClaimsPrincipalAccessorService : IClaimsPrincipalAccessorService
{
	private readonly ILogger _logger;
	private readonly IServiceProvider _serviceProvider;
	private readonly Lazy<IHttpContextService> _httpContextService;

	public ClaimsPrincipalAccessorService(
		ILogger<ClaimsPrincipalAccessorService> logger,
		IServiceProvider serviceProvider,
		Lazy<IHttpContextService> httpContextService)
	{
		_serviceProvider = serviceProvider;
		_httpContextService = httpContextService;
		_logger = logger;
	}

	public async ValueTask<ClaimsPrincipal> GetAsync()
	{
		try
		{
			AuthenticationStateProvider? authenticationStateProvider = _serviceProvider.GetService<AuthenticationStateProvider>();

			if (authenticationStateProvider is not null)
			{
				var authState = await authenticationStateProvider.GetAuthenticationStateAsync();

				return authState.User;
			}

			return _httpContextService.Value.User ?? new ClaimsPrincipal(new ClaimsIdentity());
		}
		catch (Exception exc) when (_logger.WriteError(exc))
		{
			throw new UmbrellaException("There has been a problem retrieving the ClaimsPrincipal.", exc);
		}
	}
}