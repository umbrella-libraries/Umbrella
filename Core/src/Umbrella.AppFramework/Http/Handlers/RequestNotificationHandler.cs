
using System.Net.Http;
using Umbrella.AppFramework.Http.Handlers.Options;
using Umbrella.AppFramework.Services.Abstractions;

namespace Umbrella.AppFramework.Http.Handlers;

/// <summary>
/// A <see cref="DelegatingHandler"/> for use with the <see cref="IHttpClientFactory"/> infrastructure which notifies users
/// that there is a request in progress by displaying and hiding a loading indicator.
/// </summary>
/// <seealso cref="DelegatingHandler" />
public class RequestNotificationHandler : DelegatingHandler
{
	private readonly RequestNotificationHandlerOptions _options;
	private readonly ILoadingScreenService _loadingScreenUtility;

	/// <summary>
	/// Initializes a new instance of the <see cref="RequestNotificationHandler"/> class.
	/// </summary>
	/// <param name="options">The options.</param>
	/// <param name="loadingScreenUtility">The loading screen utility.</param>
	public RequestNotificationHandler(
		RequestNotificationHandlerOptions options,
		ILoadingScreenService loadingScreenUtility)
	{
		_options = options;
		_loadingScreenUtility = loadingScreenUtility;
	}

	/// <inheritdoc />
	protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		bool isMatch = _options.Exclusions.Any(x => x.PathMatchType switch
		{
			RequestNotificationHandlerPathMatchType.Exact => request.RequestUri?.AbsolutePath.Equals(x.Path, StringComparison.OrdinalIgnoreCase) is true && request.Method == x.Method,
			RequestNotificationHandlerPathMatchType.StartsWith => request.RequestUri?.AbsolutePath.StartsWith(x.Path, StringComparison.OrdinalIgnoreCase) is true && request.Method == x.Method,
			_ => throw new NotSupportedException($"The type {x.PathMatchType} is not supported.")
		});

		if (isMatch)
			return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

		try
		{
			_loadingScreenUtility.Show();

			HttpResponseMessage response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

			return response;
		}
		finally
		{
			// TODO: Potential issue somewhere here where the loading screen sometimes gets stuck and never disappears.
			// Need to figure out if we can set a timeout on the request or loading screen so it auto-hides after a few minutes.
			_loadingScreenUtility.Hide();
		}
	}
}