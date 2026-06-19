using System.Security.Claims;

namespace Umbrella.AspNetCore.Shared.Services.Abstractions;

/// <summary>
/// Defines an interface for a service that provides access to the current <see cref="ClaimsPrincipal"/> in an ASP.NET
/// Core application.
/// </summary>
public interface IClaimsPrincipalAccessorService
{
	/// <summary>
	/// Gets the current <see cref="ClaimsPrincipal"/> asynchronously.
	/// </summary>
	/// <returns>The current <see cref="ClaimsPrincipal"/>.</returns>
	ValueTask<ClaimsPrincipal> GetAsync();
}