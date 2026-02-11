
using System.Security.Claims;
using CommunityToolkit.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Umbrella.Utilities.Exceptions;
using Umbrella.Utilities.Security.Abstractions;

namespace Umbrella.AspNetCore.WebUtilities.Mvc.Services;

// TODO: Investigate if we can change the authorization for the File System to build on top of this instead of
// using a custom implementation.

/// <summary>
/// Checks policy based permissions for a user
/// </summary>
public class UmbrellaAspNetAuthorizationService : IUmbrellaAuthorizationService
{
	private readonly ILogger _logger;
	private readonly IAuthorizationService _authorizationService;

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaAspNetAuthorizationService"/> class.
	/// </summary>
	/// <param name="logger">The logger used to log information, warnings, and errors related to authorization operations.</param>
	/// <param name="authorizationService">The service used to perform authorization checks.</param>
	public UmbrellaAspNetAuthorizationService(
		ILogger<UmbrellaAspNetAuthorizationService> logger,
		IAuthorizationService authorizationService)
	{
		_logger = logger;
		_authorizationService = authorizationService;
	}

	/// <inheritdoc />
	public async Task<bool> AuthorizeAsync(ClaimsPrincipal user, object? resource, string policyName, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(user);

		try
		{
			var result = await _authorizationService.AuthorizeAsync(user, resource, policyName).ConfigureAwait(false);

			return result.Succeeded;
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { user.Identity?.Name, policyName }))
		{
			throw;
		}
	}

	/// <inheritdoc />
	public async Task<bool> AuthorizeAllAsync<TResource>(ClaimsPrincipal user, IEnumerable<TResource> resources, string policyName, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(user);

		try
		{
			return await _authorizationService.AuthorizeAllAsync(user, resources, policyName, cancellationToken);
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { user.Identity?.Name, policyName }))
		{
			throw new UmbrellaException("There has been a problem performing the authorization operation.");
		}
	}
}