
using System.Security.Claims;
using CommunityToolkit.Diagnostics;

namespace Umbrella.WebUtilities.Middleware.Options;

/// <summary>
/// Options for the MultiTenantSessionContextMiddleware in the ASP.NET and ASP.NET Core projects.
/// </summary>
public class MultiTenantSessionContextMiddlewareOptions
{
	/// <summary>
	/// <para>
	/// Gets or sets the type of the tenant claim as stored on the <see cref="ClaimsPrincipal"/> of the currently authenticated user.
	/// Defaults to <see cref="ClaimTypes.GroupSid"/>.
	/// </para>
	/// <para>
	/// The value of this claim will be read from the current user's claims and assigned to the scoped instance of DbAppTenantSessionContext registered
	/// with the application's dependency injection container. The primary use of this would then be to perform a row filtering operation when accessing data to ensure
	/// that data cannot bleed across different tenants.
	/// </para>
	/// </summary>
	public string TenantClaimType { get; set; } = ClaimTypes.GroupSid;

	/// <inheritdoc />
	public void Sanitize() => TenantClaimType = TenantClaimType.Trim();

	/// <inheritdoc />
	public void Validate() => Guard.IsNotNullOrWhiteSpace(TenantClaimType, nameof(TenantClaimType));
}