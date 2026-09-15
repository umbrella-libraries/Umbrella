using CommunityToolkit.Diagnostics;
using Umbrella.Utilities.Options.Abstractions;

namespace Umbrella.AppFramework.Security.Options;

/// <summary>
/// Options for use with the <see cref="AppAuthTokenStorageService"/> class.
/// </summary>
/// <seealso cref="ISanitizableUmbrellaOptions" />
/// <seealso cref="IValidatableUmbrellaOptions" />
public class AppAuthTokenStorageServiceOptions : ISanitizableUmbrellaOptions, IValidatableUmbrellaOptions
{
	/// <summary>
	/// Gets or sets the authentication token storage key.
	/// </summary>
	/// <remarks>Defaults to <c>App.AuthToken</c></remarks>
	public string AuthTokenStorageKey { get; set; } = "App.AuthToken";

	/// <summary>
	/// Gets or sets the client identifier storage key.
	/// </summary>
	/// <remarks>Defaults to <c>App.ClientId</c></remarks>
	public string ClientIdStorageKey { get; set; } = "App.ClientId";

	/// <summary>
	/// Gets or sets a value indicating whether the auth token should be stored in local storage, or scoped to the session. If session storage
	/// is not supported on the target platform, e.g. MAUI, then local storage will be used instead which means this value is effectively ignored.
	/// </summary>
	/// <remarks>Defaults to <see langword="true"/>.</remarks>
	public bool UseAuthTokenLocalStorage { get; set; } = true;

	/// <inheritdoc/>
	public void Sanitize()
	{
		AuthTokenStorageKey = AuthTokenStorageKey?.Trim() ?? string.Empty;
		ClientIdStorageKey = ClientIdStorageKey?.Trim() ?? string.Empty;
	}

	/// <inheritdoc/>
	public void Validate()
	{
		Guard.IsNotNullOrEmpty(AuthTokenStorageKey);
		Guard.IsNotNullOrEmpty(ClientIdStorageKey);
	}
}
