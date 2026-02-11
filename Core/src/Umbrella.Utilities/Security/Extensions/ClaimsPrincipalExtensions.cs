


using System.Globalization;
using System.Security.Claims;
using System.Text;
using Umbrella.Utilities.Exceptions;
using Umbrella.Utilities.Extensions;
using Umbrella.Utilities.TypeConverters;

namespace Umbrella.Utilities.Security.Extensions;

/// <summary>
/// Extension methods for the <see cref="ClaimsPrincipal"/> type.
/// </summary>
public static class ClaimsPrincipalExtensions
{
	/// <summary>
	/// Gets the role names.
	/// </summary>
	/// <param name="principal">The principal.</param>
	/// <param name="roleClaimType">Type of the role claim.</param>
	/// <returns>The collection of roles.</returns>
	public static IReadOnlyCollection<string> GetRoleNames(this ClaimsPrincipal principal, string roleClaimType = ClaimTypes.Role)
		=> principal.Claims.Where(x => x.Type == roleClaimType).Select(x => x.Value).ToArray();

	/// <summary>
	/// Gets the full name.
	/// </summary>
	/// <param name="principal">The principal.</param>
	/// <param name="givenNameClaimType">Type of the given name claim.</param>
	/// <param name="surnameClaimType">Type of the surname claim.</param>
	/// <returns>The full name.</returns>
	public static string? GetFullName(this ClaimsPrincipal principal, string givenNameClaimType = ClaimTypes.GivenName, string surnameClaimType = ClaimTypes.Surname)
	{
		string? firstName = principal.FindFirst(givenNameClaimType)?.Value;
		string? lastName = principal.FindFirst(surnameClaimType)?.Value;

		if (string.IsNullOrEmpty(firstName) && string.IsNullOrEmpty(lastName))
			return null;

		return $"{firstName} {lastName}";
	}

	/// <summary>
	/// Gets the first name.
	/// </summary>
	/// <param name="principal">The principal.</param>
	/// <param name="givenNameClaimType">Type of the given name claim.</param>
	/// <returns>The first name.</returns>
	public static string? GetFirstName(this ClaimsPrincipal principal, string givenNameClaimType = ClaimTypes.GivenName) => principal.FindFirst(givenNameClaimType)?.Value;

	/// <summary>
	/// Gets the last name.
	/// </summary>
	/// <param name="principal">The principal.</param>
	/// <param name="surnameClaimType">Type of the surname claim.</param>
	/// <returns>The last name.</returns>
	public static string? GetLastName(this ClaimsPrincipal principal, string surnameClaimType = ClaimTypes.Surname) => principal.FindFirst(surnameClaimType)?.Value;

	/// <summary>
	/// Gets the initials using the first and last name claims.
	/// </summary>
	/// <param name="principal">The principal.</param>
	/// <param name="givenNameClaimType">Type of the given name claim.</param>
	/// <param name="surnameClaimType">Type of the surname claim.</param>
	/// <returns>The initials.</returns>
	public static string? GetInitials(this ClaimsPrincipal principal, string givenNameClaimType = ClaimTypes.GivenName, string surnameClaimType = ClaimTypes.Surname)
	{
		string? firstName = principal.GetFirstName(givenNameClaimType);
		string? lastName = principal.GetLastName(surnameClaimType);

		var sbInitials = new StringBuilder();

		if (!string.IsNullOrWhiteSpace(firstName))
			_ = sbInitials.Append(firstName![0]);

		if (!string.IsNullOrWhiteSpace(lastName))
			_ = sbInitials.Append(lastName![0]);

		return sbInitials.ToString();
	}

	/// <summary>
	/// Gets the mobile phone number.
	/// </summary>
	/// <param name="principal">The principal.</param>
	/// <param name="mobilePhoneNumberClaimType">Type of the mobile phone number claim.</param>
	/// <returns>The mobile phone number.</returns>
	public static string? GetMobilePhoneNumber(this ClaimsPrincipal principal, string mobilePhoneNumberClaimType = ClaimTypes.MobilePhone) => principal.FindFirst(mobilePhoneNumberClaimType)?.Value;

	/// <summary>
	/// Gets the identifier.
	/// </summary>
	/// <typeparam name="TUserId">The type of the user identifier.</typeparam>
	/// <param name="principal">The principal.</param>
	/// <param name="nameIdentifierClaimType">Type of the name identifier claim.</param>
	/// <returns>The identifier</returns>
	public static TUserId GetId<TUserId>(this ClaimsPrincipal principal, string nameIdentifierClaimType = ClaimTypes.NameIdentifier)
	{
		if (principal.Identity?.IsAuthenticated is not true)
			return default!;

		Claim? idClaim = principal.FindFirst(nameIdentifierClaimType);

		if (idClaim is null)
			throw new UmbrellaException("The NameIdentifier claim could not be found. This should not be possible.");

		Type userIdType = typeof(TUserId);

		if (userIdType == typeof(string))
			return (TUserId)(object)idClaim.Value;

		Type targetType = userIdType;

		if (userIdType.IsNullableType())
			targetType = userIdType.GetGenericArguments()[0];

		return (TUserId)Convert.ChangeType(idClaim.Value, targetType, CultureInfo.InvariantCulture);
	}

	/// <summary>
	/// Determines whether the current principal is in the specified role.
	/// </summary>
	/// <typeparam name="TRole">The type of the role.</typeparam>
	/// <param name="principal">The principal.</param>
	/// <param name="roleType">Type of the role.</param>
	/// <returns><see langword="true"/> if yes; otherwise <see langword="false"/>.</returns>
	public static bool IsInRole<TRole>(this ClaimsPrincipal principal, TRole roleType)
		where TRole : struct, Enum
		=> principal.IsInRole(roleType.ToString());

	/// <summary>
	/// Determines whether the current principal is in any of the specified <paramref name="roles"/>.
	/// </summary>
	/// <typeparam name="TAppRoleType">The type of the application role type.</typeparam>
	/// <param name="claimsPrincipal">The claims principal.</param>
	/// <param name="roles">The roles.</param>
	/// <returns>
	///   <see langword="true"/> if yes; otherwise <see langword="false"/>.
	/// </returns>
	public static bool IsInAnyRoles<TAppRoleType>(this ClaimsPrincipal claimsPrincipal, params TAppRoleType[] roles)
		where TAppRoleType : struct, Enum
		=> roles.Any(x => claimsPrincipal.IsInRole(x));

	/// <summary>
	/// Determines whether the current principal is in all of the specified <paramref name="roles"/>.
	/// </summary>
	/// <typeparam name="TAppRoleType">The type of the application role type.</typeparam>
	/// <param name="claimsPrincipal">The claims principal.</param>
	/// <param name="roles">The roles.</param>
	/// <returns>
	///   <see langword="true"/> if yes; otherwise <see langword="false"/>.
	/// </returns>
	public static bool IsInAllRoles<TAppRoleType>(this ClaimsPrincipal claimsPrincipal, params TAppRoleType[] roles)
		where TAppRoleType : struct, Enum
		=> roles.All(x => claimsPrincipal.IsInRole(x));

	/// <summary>
	/// Gets the roles.
	/// </summary>
	/// <typeparam name="TRole">The type of the role.</typeparam>
	/// <param name="principal">The principal.</param>
	/// <param name="roleClaimType">Type of the role claim.</param>
	/// <returns>The roles.</returns>
	public static IReadOnlyCollection<TRole> GetRoles<TRole>(this ClaimsPrincipal principal, string roleClaimType = ClaimTypes.Role)
		where TRole : struct, Enum
	{
		var lstRole = new List<TRole>();

		foreach (var roleClaim in principal.Claims.Where(x => x.Type == roleClaimType))
		{
			if (Enum.TryParse(roleClaim.Value, true, out TRole result))
				lstRole.Add(result);
		}

		return lstRole;
	}

	/// <summary>
	/// Find the first claim value of the specified type and convert it to an instance of type <typeparamref name="T"/>.
	/// </summary>
	/// <typeparam name="T">The type to convert to.</typeparam>
	/// <param name="claimsPrincipal">The claims principal.</param>
	/// <param name="claimType">The type of the claim.</param>
	/// <param name="fallbackCreator">
	/// The fallback creator.
	/// This is used to provide a default value in cases where the claim's value is null or empty.
	/// In cases where no fallback has been specified, the <see langword="default"/> value for <typeparamref name="T"/> will be returned EXCEPT if <typeparamref name="T"/>
	/// is a <see langword="string"/> in which case <see cref="string.Empty"/> will be returned.
	/// </param>
	/// <param name="customValueConverter">The custom value converter.</param>
	/// <returns>The conversion result.</returns>
	public static T GetFirstValueOrDefault<T>(this ClaimsPrincipal claimsPrincipal, string claimType, Func<T> fallbackCreator, Func<string?, T>? customValueConverter = null)
	{
		Claim? claim = claimsPrincipal.FindFirst(claimType);

		return GenericTypeConverterHelper.Convert(claim?.Value, fallbackCreator, customValueConverter);
	}

	/// <summary>
	/// Find the first claim value of the specified type and convert it to an instance of type <typeparamref name="T"/>.
	/// </summary>
	/// <typeparam name="T">The type to convert to.</typeparam>
	/// <param name="claimsPrincipal">The claims principal.</param>
	/// <param name="claimType">The type of the claim.</param>
	/// <param name="fallback">
	/// The fallback. This is used to provide a default value in cases where the claim's value is null or empty.
	/// In cases where no fallback has been specified, the <see langword="default"/> value for <typeparamref name="T"/> will be returned EXCEPT if <typeparamref name="T"/>
	/// is a <see langword="string"/> in which case <see cref="string.Empty"/> will be returned.
	/// </param>
	/// <param name="customValueConverter">The custom value converter.</param>
	/// <returns>The conversion result.</returns>
	public static T GetFirstValueOrDefault<T>(this ClaimsPrincipal claimsPrincipal, string claimType, T fallback = default!, Func<string?, T>? customValueConverter = null)
	{
		Claim? claim = claimsPrincipal.FindFirst(claimType);

		return GenericTypeConverterHelper.Convert(claim?.Value, fallback, customValueConverter);
	}

	/// <summary>
	/// Find the first claim value of the specified type and convert it to an <see langword="enum"/> instance of type <typeparamref name="T"/>.
	/// </summary>
	/// <typeparam name="T">The <see langword="enum"/></typeparam>
	/// <param name="claimsPrincipal">The claims principal.</param>
	/// <param name="claimType">The type of the claim.</param>
	/// <param name="fallback">The fallback which is returned if the value is null, empty or whitespace, or if the value cannot be converted to the specified enum <typeparamref name="T"/>.</param>
	/// <returns>The conversion result.</returns>
	public static T GetFirstEnumValueOrDefault<T>(this ClaimsPrincipal claimsPrincipal, string claimType, T fallback = default)
		where T : struct, Enum
	{
		Claim? claim = claimsPrincipal.FindFirst(claimType);

		return GenericTypeConverterHelper.ConvertToEnum(claim?.Value, fallback);
	}

	/// <summary>
	/// Find the first claim value of the specified type and convert it to a nullable <see langword="enum"/> instance of type <typeparamref name="T"/>.
	/// </summary>
	/// <typeparam name="T">The <see langword="enum"/></typeparam>
	/// <param name="claimsPrincipal">The claims principal.</param>
	/// <param name="claimType">The type of the claim.</param>
	/// <param name="fallback">The fallback which is returned if the value is null, empty or whitespace, or if the value cannot be converted to the specified enum <typeparamref name="T"/>.</param>
	/// <returns>The conversion result.</returns>
	public static T? GetFirstNullableEnumValueOrDefault<T>(this ClaimsPrincipal claimsPrincipal, string claimType, T? fallback = null)
		where T : struct, Enum
		=> GenericTypeConverterHelper.ConvertToNullableEnum(claimsPrincipal.FindFirst(claimType)?.Value, fallback);

	/// <summary>
	/// Determines if a claim exists for the specified <paramref name="claimType"/> with a value of <see langword="true"/>.
	/// </summary>
	/// <param name="claimsPrincipal">The claims principal.</param>
	/// <param name="claimType">The type of the claim.</param>
	/// <returns><see langword="true"/> if a claim exists with a <see langword="true"/> value; otherwise <see langword="false"/>.</returns>
	public static bool HasBooleanTrueClaim(this ClaimsPrincipal claimsPrincipal, string claimType)
		=> claimsPrincipal.FindFirst(claimType)?.Value?.Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase) is true;
}