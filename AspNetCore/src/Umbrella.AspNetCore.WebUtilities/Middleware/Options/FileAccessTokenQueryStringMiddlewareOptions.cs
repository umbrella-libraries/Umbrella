


using CommunityToolkit.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using Umbrella.Utilities.Options.Abstractions;

namespace Umbrella.AspNetCore.WebUtilities.Middleware.Options;

/// <summary>
/// Options for the FileAccessTokenQueryStringMiddleware.
/// </summary>
/// <seealso cref="ISanitizableUmbrellaOptions" />
/// <seealso cref="IValidatableUmbrellaOptions" />
public class FileAccessTokenQueryStringMiddlewareOptions : IValidatableUmbrellaOptions
{
	/// <summary>
	/// Gets or sets the validation parameters.
	/// </summary>
	public TokenValidationParameters ValidationParameters { get; set; } = null!;

	/// <inheritdoc/>
	public void Validate() => Guard.IsNotNull(ValidationParameters, nameof(ValidationParameters));
}