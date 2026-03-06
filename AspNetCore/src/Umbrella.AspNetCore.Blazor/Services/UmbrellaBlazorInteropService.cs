using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Umbrella.AspNetCore.Blazor.Services.Abstractions;

namespace Umbrella.AspNetCore.Blazor.Services;

/// <summary>
/// A service containing core interop functionality between Blazor and JavaScript for features not yet supported
/// natively by Blazor.
/// </summary>
/// <seealso cref="IUmbrellaBlazorInteropService"/>
public class UmbrellaBlazorInteropService : IUmbrellaBlazorInteropService
{
	private readonly ILogger _logger;
	private readonly IJSRuntime _jsRuntime;
	private readonly DotNetObjectReference<UmbrellaBlazorInteropService> _interopReference;
	private readonly List<AwaitableBlazorEventHandler> _windowScrolledTopEventHandlerList = [];

	/// <inheritdoc />
	public event AwaitableBlazorEventHandler OnWindowScrolledTop
	{
		add
		{
			_windowScrolledTopEventHandlerList.Add(value);

			if (_windowScrolledTopEventHandlerList.Count is 1)
				_ = InitializeWindowScrolledTopAsync();
		}
		remove
		{
			_ = _windowScrolledTopEventHandlerList.Remove(value);

			if (_windowScrolledTopEventHandlerList.Count is 0)
				_ = DestroyWindowScrolledTopAsync();
		}
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaBlazorInteropService"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="jsRuntime">The js runtime.</param>
	public UmbrellaBlazorInteropService(
		ILogger<UmbrellaBlazorInteropService> logger,
		IJSRuntime jsRuntime)
	{
		_logger = logger;
		_jsRuntime = jsRuntime;
		_interopReference = DotNetObjectReference.Create(this);
	}

	/// <inheritdoc />
	public async ValueTask SetPageTitleAsync(string pageTitle)
	{
		try
		{
			await _jsRuntime.InvokeVoidAsync("UmbrellaBlazorInterop.setPageTitle", pageTitle);
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { pageTitle }))
		{
			// Do nothing here
		}
	}

	/// <inheritdoc />
	public async ValueTask ScrollToAsync(int scrollY, int offset = 0)
	{
		try
		{
			await _jsRuntime.InvokeVoidAsync("UmbrellaBlazorInterop.scrollTo", scrollY, offset);
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { scrollY }))
		{
			// Do nothing here
		}
	}

	/// <inheritdoc />
	public async ValueTask ScrollToAsync(string elementSelector, int offset = 0)
	{
		try
		{
			await _jsRuntime.InvokeVoidAsync("UmbrellaBlazorInterop.scrollTo", elementSelector, offset);
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { elementSelector }))
		{
			// Do nothing here
		}
	}

	/// <inheritdoc />
	public async ValueTask ScrollToBottomAsync()
	{
		try
		{
			await _jsRuntime.InvokeVoidAsync("UmbrellaBlazorInterop.scrollToBottom");
		}
		catch (Exception exc) when (_logger.WriteError(exc))
		{
			// Do nothing here
		}
	}

	/// <inheritdoc />
	public async ValueTask TriggerElementClickAsync(string elementSelector)
	{
		try
		{
			await _jsRuntime.InvokeVoidAsync("UmbrellaBlazorInterop.triggerElementClick", elementSelector);
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { elementSelector }))
		{
			// Do nothing here
		}
	}

	/// <inheritdoc />
	[JSInvokable]
	public async ValueTask OnWindowScrolledTopAsync() => await Task.WhenAll(_windowScrolledTopEventHandlerList.Select(x => x.Invoke()));

	private async Task InitializeWindowScrolledTopAsync() => await _jsRuntime.InvokeVoidAsync("UmbrellaBlazorInterop.initializeWindowScrolledTopAsync", _interopReference, 10);

	private async Task DestroyWindowScrolledTopAsync() => await _jsRuntime.InvokeVoidAsync("UmbrellaBlazorInterop.destroyWindowScrolledTopAsync");

	/// <inheritdoc />
	public async ValueTask OpenUrlAsync(string url, string target, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			await _jsRuntime.InvokeVoidAsync("open", cancellationToken, url, target);
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { url, target }))
		{
			throw new UmbrellaBlazorException("There has been a problem opening the specified URL.", exc);
		}
	}
}