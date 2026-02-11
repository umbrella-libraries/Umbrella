using System.Net.Http;
using System.Text;
using CommunityToolkit.Diagnostics;
using Umbrella.AppFramework.Security.Abstractions;
using Umbrella.AppFramework.Shared.Constants;
using Umbrella.Utilities;
using Umbrella.Utilities.Http;

namespace Umbrella.AppFramework.Http.Handlers;

/// <summary>
/// A <see cref="DelegatingHandler"/> for use with the <see cref="IHttpClientFactory"/> infrastructure which
/// refreshes the auth session if the response contains a new auth token.
/// </summary>
/// <seealso cref="DelegatingHandler" />
public class RefreshedAuthTokenHandler : DelegatingHandler
{
	private readonly IAppAuthHelper _authHelper;

	/// <summary>
	/// Initializes a new instance of the <see cref="RefreshedAuthTokenHandler"/> class.
	/// </summary>
	/// <param name="authHelper">The authentication helper.</param>
	public RefreshedAuthTokenHandler(
		IAppAuthHelper authHelper)
	{
		_authHelper = authHelper;
	}

	/// <inheritdoc />
	protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(request);

		var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

		if (response.Headers.TryGetValues(AppHttpHeaderName.NewAuthToken, out var values))
		{
			string? token = values.FirstOrDefault()?.Trim();

			if (!string.IsNullOrWhiteSpace(token))
				_ = await _authHelper.SetCurrentClaimsPrincipalAsync(token!, cancellationToken).ConfigureAwait(false);
		}
		else if (response.StatusCode is HttpStatusCode.Unauthorized && request.RequestUri?.ToString().EndsWith("/auth/login", StringComparison.OrdinalIgnoreCase) is false)
		{
			string json = UmbrellaStatics.SerializeJson(new HttpProblemDetails { Title = "Logged Out", Detail = "You have been logged out due to inactivity. Please login again to continue." });
			response.Content = new StringContent(json, Encoding.UTF8, "application/problem+json");

			await _authHelper.LocalLogoutAsync(true, cancellationToken).ConfigureAwait(false);
		}

		return response;
	}
}