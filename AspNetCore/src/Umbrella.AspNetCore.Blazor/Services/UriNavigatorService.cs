using Microsoft.Extensions.Logging;
using Umbrella.AppFramework.Services.Abstractions;
using Umbrella.AspNetCore.Blazor.Services.Abstractions;

namespace Umbrella.AspNetCore.Blazor.Services;

/// <summary>
/// A service used to perform navigation to a specific URI.
/// </summary>
/// <remarks>
/// Navigation within the same window works in all render modes including static rendering / prerendering.
/// Opening a URI in a new window requires an interactive render mode (Interactive Server or WebAssembly)
/// because it relies on JavaScript interop.
/// </remarks>
public class UriNavigatorService : IUriNavigatorService
{
	private readonly ILogger _logger;
	private readonly NavigationManager _navigationManager;
	private readonly IUmbrellaBlazorInteropService _interopService;

	/// <summary>
	/// Initializes a new instance of the <see cref="UriNavigatorService"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="navigationManager">The navigation manager.</param>
	/// <param name="interopService">The interop service used to open URIs in a new browser window.</param>
	public UriNavigatorService(
		ILogger<UriNavigatorService> logger,
		NavigationManager navigationManager,
		IUmbrellaBlazorInteropService interopService)
	{
		_logger = logger;
		_navigationManager = navigationManager;
		_interopService = interopService;
	}

	/// <inheritdoc />
	/// <remarks>
	/// When <paramref name="openInNewWindow"/> is <see langword="true"/> the call is delegated to
	/// <see cref="IUmbrellaBlazorInteropService.OpenUrlAsync"/> which requires interactive rendering.
	/// Calling this overload with <paramref name="openInNewWindow"/> = <see langword="true"/> during
	/// static rendering / prerendering will throw an <see cref="UmbrellaBlazorException"/>.
	/// </remarks>
	public async ValueTask OpenAsync(string uri, bool openInNewWindow)
	{
		try
		{
			if (openInNewWindow)
				await _interopService.OpenUrlAsync(uri, "_blank");
			else
				_navigationManager.NavigateTo(uri);
		}
		catch (UmbrellaBlazorException)
		{
			throw;
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { uri, openInNewWindow }))
		{
			throw new UmbrellaBlazorException("There has been a problem opening the specified URI.", exc);
		}
	}
}