using System.Net.Http;
using System.Net.Http.Headers;
using Umbrella.AppFramework.Security.Abstractions;

namespace Umbrella.AppFramework.Http.Handlers;

/// <summary>
/// A <see cref="DelegatingHandler"/> for use with the <see cref="IHttpClientFactory"/> infrastructure which
/// attaches the auth token for the current user to the request.
/// </summary>
/// <seealso cref="DelegatingHandler" />
public class AuthTokenHandler : DelegatingHandler
{
	private readonly IAppAuthTokenStorageService _tokenStorageService;

	/// <summary>
	/// Initializes a new instance of the <see cref="AuthTokenHandler"/> class.
	/// </summary>
	/// <param name="tokenStorageService">The token storage service.</param>
	public AuthTokenHandler(IAppAuthTokenStorageService tokenStorageService)
	{
		_tokenStorageService = tokenStorageService;
	}

	/// <inheritdoc />
	protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (request is null)
			throw new ArgumentNullException(nameof(request));

		string? token = await _tokenStorageService.GetTokenAsync().ConfigureAwait(false);

		if (!string.IsNullOrEmpty(token))
			request.Headers.Authorization = new AuthenticationHeaderValue("bearer", token);

		return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
	}
}
