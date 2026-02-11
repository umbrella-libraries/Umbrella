
using System.Security.Claims;
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Umbrella.AppFramework.Security.Messages;

/// <summary>
/// A message that signals when the authentication state for the current <see cref="ClaimsPrincipal"/> has changed.
/// </summary>
/// <seealso cref="ValueChangedMessage{ClaimsPrincipal}" />
public class AuthenticationStateChangedMessage : ValueChangedMessage<ClaimsPrincipal>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="AuthenticationStateChangedMessage"/> class.
	/// </summary>
	/// <param name="value">The value that has changed.</param>
	public AuthenticationStateChangedMessage(ClaimsPrincipal value)
		: base(value)
	{
	}
}