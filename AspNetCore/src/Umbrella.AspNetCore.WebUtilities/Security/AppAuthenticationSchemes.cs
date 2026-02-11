


namespace Umbrella.AspNetCore.WebUtilities.Security;

/// <summary>
/// Specifies the names of custom app authentication schemes.
/// </summary>
public static class AppAuthenticationSchemes
{
	/// <summary>
	/// The scheme for bearer tokens used for two-factor authentication.
	/// </summary>
	public const string Bearer2FA = nameof(Bearer2FA);

	/// <summary>
	/// The scheme for bearer tokens used for file access.
	/// </summary>
	[Obsolete("This will be removed in a future version because it is no longer needed.")]
	public const string BearerFAT = nameof(BearerFAT);
}