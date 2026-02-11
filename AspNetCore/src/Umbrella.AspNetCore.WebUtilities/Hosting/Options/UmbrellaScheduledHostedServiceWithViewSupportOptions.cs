
using CommunityToolkit.Diagnostics;
using Microsoft.AspNetCore.Http;
using Umbrella.Utilities.Options.Abstractions;

namespace Umbrella.AspNetCore.WebUtilities.Hosting.Options;

/// <summary>
/// Options for use with the <see cref="UmbrellaScheduledHostedServiceWithViewSupportBase"/>
/// </summary>
/// <seealso cref="ISanitizableUmbrellaOptions" />
/// <seealso cref="IValidatableUmbrellaOptions" />
public class UmbrellaScheduledHostedServiceWithViewSupportOptions : ISanitizableUmbrellaOptions, IValidatableUmbrellaOptions
{
	/// <summary>
	/// Gets or sets the content URL scheme.
	/// </summary>
	/// <remarks>
	/// This is assigned to <see cref="HttpRequest.Scheme" />.
	/// </remarks>
	public string ContentUrlScheme { get; set; } = null!;

	/// <summary>
	/// Gets or sets the content URL host.
	/// </summary>
	/// <remarks>
	/// This is assigned to <see cref="HttpRequest.Host" />.
	/// </remarks>
	public string ContentUrlHost { get; set; } = null!;

	/// <summary>
	/// Gets or sets the default language culture code.
	/// </summary>
	/// <remarks>
	/// This is assigned to the <c>Accept-Language</c> header in the <see cref="HttpRequest.Headers"/> and also
	/// to the <c>CurrentCulture</c> property of <see cref="Thread.CurrentThread"/>.
	/// </remarks>
	public string DefaultLanguageCultureCode { get; set; } = null!;

	/// <summary>
	/// Gets or sets the default language UI culture code.
	/// </summary>
	/// <remarks>
	/// This is assigned to the <c>CurrentUICulture</c> property of <see cref="Thread.CurrentThread"/>.
	/// </remarks>
	public string DefaultLanguageUICultureCode { get; set; } = null!;

	/// <inheritdoc/>
	public void Sanitize()
	{
		ContentUrlScheme = ContentUrlScheme?.Trim()!;
		ContentUrlHost = ContentUrlHost?.Trim()!;
		DefaultLanguageCultureCode = DefaultLanguageCultureCode?.Trim()!;
		DefaultLanguageUICultureCode = DefaultLanguageUICultureCode?.Trim()!;
	}

	/// <inheritdoc/>
	public void Validate()
	{
		Guard.IsNotNullOrEmpty(ContentUrlScheme);
		Guard.IsNotNullOrEmpty(ContentUrlHost);
		Guard.IsNotNullOrEmpty(DefaultLanguageCultureCode);
		Guard.IsNotNullOrEmpty(DefaultLanguageUICultureCode);
	}
}