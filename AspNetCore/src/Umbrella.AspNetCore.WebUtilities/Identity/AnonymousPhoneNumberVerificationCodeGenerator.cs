using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Umbrella.AspNetCore.WebUtilities.Identity.Abstractions;
using Umbrella.AspNetCore.WebUtilities.Identity.Options;
using Umbrella.WebUtilities.Exceptions;

namespace Umbrella.AspNetCore.WebUtilities.Identity;

/// <summary>
/// A utility used to generate codes which can be used to verify the phone numbers of anonymous users.
/// </summary>
/// <typeparam name="TUserManager">The type of the user manager.</typeparam>
/// <typeparam name="TUser">The type of the user.</typeparam>
/// <typeparam name="TUserKey">The type of the user key.</typeparam>
/// <seealso cref="IAnonymousPhoneNumberVerificationCodeGenerator" />
public class AnonymousPhoneNumberVerificationCodeGenerator<TUserManager, TUser, TUserKey> : IAnonymousPhoneNumberVerificationCodeGenerator
	where TUser : IdentityUser<TUserKey>, new()
	where TUserManager : UserManager<TUser>
	where TUserKey : IEquatable<TUserKey>
{
	private readonly TUser _appUser;
	private readonly ILogger _logger;
	private readonly TUserManager _userManager;

	/// <summary>
	/// Initializes a new instance of the <see cref="AnonymousPhoneNumberVerificationCodeGenerator{TUserManager, TUser, TUserKey}"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="userManager">The user manager.</param>
	/// <param name="options">The options.</param>
	public AnonymousPhoneNumberVerificationCodeGenerator(
		ILogger<AnonymousPhoneNumberVerificationCodeGenerator<TUserManager, TUser, TUserKey>> logger,
		TUserManager userManager,
		AnonymousPhoneNumberVerificationCodeGeneratorOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		_logger = logger;
		_userManager = userManager;

		_appUser = new TUser
		{
			SecurityStamp = options.AnonymousSecurityStamp
		};
	}

	/// <inheritdoc />
	public async Task<string> CreateAsync(string phoneNumber)
	{
		try
		{
			return await _userManager.GenerateUserTokenAsync(_appUser, TokenOptions.DefaultPhoneProvider, AnonymousPhoneNumberVerificationCodeGenerator<TUserManager, TUser, TUserKey>.CreatePurpose(phoneNumber)).ConfigureAwait(false);
		}
		catch (Exception exc) when (_logger.WriteError(exc))
		{
			throw new UmbrellaWebException("There has been a problem creating the code.", exc);
		}
	}

	/// <inheritdoc />
	public async Task<bool> VerifyAsync(string phoneNumber, string code)
	{
		try
		{
			return await _userManager.VerifyUserTokenAsync(_appUser, TokenOptions.DefaultPhoneProvider, AnonymousPhoneNumberVerificationCodeGenerator<TUserManager, TUser, TUserKey>.CreatePurpose(phoneNumber), code).ConfigureAwait(false);
		}
		catch (Exception exc) when (_logger.WriteError(exc))
		{
			throw new UmbrellaWebException("There has been a problem verifying the code.", exc);
		}
	}

	private static string CreatePurpose(string phoneNumber) => $"VerifyPhoneNumber:{phoneNumber}";
}
