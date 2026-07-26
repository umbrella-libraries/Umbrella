using Microsoft.Owin;
using Umbrella.WebUtilities.Security;

namespace Umbrella.Legacy.WebUtilities.Extensions;

/// <summary>
/// Extension methods for use with the <see cref="IOwinContext"/> type.
/// </summary>
public static class IOwinContextExtensions
{
	/// <summary>
	/// Gets the current request nonce if it exists on the context.
	/// </summary>
	/// <param name="context">The context.</param>
	/// <returns>The nonce.</returns>
	public static string GetCurrentRequestNonce(this IOwinContext context)
	{
		if (context is null)
			throw new ArgumentNullException(nameof(context));

		return context.Get<string>(SecurityConstants.DefaultNonceKey);
	}
}
