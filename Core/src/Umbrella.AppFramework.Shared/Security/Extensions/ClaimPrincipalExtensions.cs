


using System.Globalization;
using System.Security.Claims;

namespace Umbrella.AppFramework.Shared.Security.Extensions;

/// <summary>
/// Extension methods for the <see cref="ClaimsPrincipal"/> type.
/// </summary>
public static class ClaimPrincipalExtensions
{
	/// <summary>
	/// Gets the file access token stored as a claim on the specified <paramref name="principal"/> with type <see cref="UmbrellaAppClaimType.FileAccessToken"/>.
	/// </summary>
	/// <param name="principal">The principal.</param>
	/// <returns>The file access token, if it exists; otherwise <see langword="null"/>.</returns>
	public static string? GetFileAccessToken(this ClaimsPrincipal principal) => principal.FindFirst(UmbrellaAppClaimType.FileAccessToken)?.Value;

	/// <summary>
	/// Gets the refresh token expiration <see cref="DateTime"/> from the claims of the
	/// specified <paramref name="principal"/> if a <see cref="UmbrellaAppClaimType.RefreshTokenExpiration"/> claim exists.
	/// </summary>
	/// <param name="principal">The principal.</param>
	/// <returns>The token expiration if it exists; otherwise <see langword="null"/>.</returns>
	public static DateTime? GetRefreshTokenExpiration(this ClaimsPrincipal principal)
	{
		string? strExpiration = principal.FindFirst(UmbrellaAppClaimType.RefreshTokenExpiration)?.Value;

		return DateTime.TryParse(strExpiration, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTime result)
			? result
			: null;
	}

	/// <summary>
	/// Gets the primary role of the specified <paramref name="principal"/> from the value of the <see cref="UmbrellaAppClaimType.PrimaryRole"/>.
	/// </summary>
	/// <typeparam name="TAppRole">The type of the application role.</typeparam>
	/// <param name="principal">The principal.</param>
	/// <returns>The primary role type. An exception is thrown if one cannot be found in the claims.</returns>
	/// <exception cref="Exception">
	/// The current principal has no primary role.
	/// or
	/// The current principal does not have a valid primary role: {value}
	/// </exception>
	public static TAppRole GetPrimaryRole<TAppRole>(this ClaimsPrincipal principal)
		where TAppRole : struct, Enum
	{
		string? value = principal.FindFirst(UmbrellaAppClaimType.PrimaryRole)?.Value;

		if (string.IsNullOrWhiteSpace(value))
			throw new UmbrellaAppFrameworkSharedException("The current principal has no primary role.");

		return !Enum.TryParse(value, out TAppRole roleType)
			? throw new UmbrellaAppFrameworkSharedException($"The current principal does not have a valid primary role: {value}")
			: roleType;
	}
}