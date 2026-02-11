


namespace Umbrella.AppFramework.Shared.Security;

/// <summary>
/// Built-in claim types used by the Umbrella AppFramework.
/// </summary>
public static class UmbrellaAppClaimType
{
	private const string ClaimPrefix = "Umbrella.";

	/// <summary>
	/// The file access token claim type.
	/// </summary>
	public const string FileAccessToken = ClaimPrefix + nameof(FileAccessToken);

	/// <summary>
	/// The refresh token claim type.
	/// </summary>
	public const string RefreshToken = ClaimPrefix + nameof(RefreshToken);

	/// <summary>
	/// The refresh token expiration claim type.
	/// </summary>
	public const string RefreshTokenExpiration = ClaimPrefix + nameof(RefreshTokenExpiration);

	/// <summary>
	/// The primary role claim type.
	/// </summary>
	public const string PrimaryRole = ClaimPrefix + nameof(PrimaryRole);
}