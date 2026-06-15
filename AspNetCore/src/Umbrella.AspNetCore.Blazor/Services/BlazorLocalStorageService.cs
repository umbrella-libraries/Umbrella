using Microsoft.Extensions.Logging;
using Umbrella.AppFramework.Services.Abstractions;

namespace Umbrella.AspNetCore.Blazor.Services;

/// <summary>
/// A persistent local storage service that stores string values using the browser's native <c>localStorage</c> API.
/// </summary>
/// <remarks>
/// This service requires an interactive render mode (Interactive Server or WebAssembly). All operations are silently
/// skipped during static rendering / prerendering, where JavaScript interop is unavailable.
/// </remarks>
/// <seealso cref="IAppLocalStorageService" />
public class BlazorLocalStorageService : IAppLocalStorageService
{
	private readonly ILogger _logger;
	private readonly IJSRuntime _js;

	/// <summary>
	/// Initializes a new instance of the <see cref="BlazorLocalStorageService"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="js">The JS runtime.</param>
	public BlazorLocalStorageService(
		ILogger<BlazorLocalStorageService> logger,
		IJSRuntime js)
	{
		_logger = logger;
		_js = js;
	}

	/// <inheritdoc />
	public async ValueTask<string?> GetAsync(string key)
	{
		try
		{
			return await _js.InvokeAsync<string?>("localStorage.getItem", key);
		}
		catch (InvalidOperationException)
		{
			// JS interop is unavailable during static rendering / prerendering; return null so callers treat it as a cache miss.
			_logger.WriteDebug(message: "Local storage read skipped: JavaScript interop is not available during prerendering.");
			return null;
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { key }))
		{
			throw new UmbrellaBlazorException("There has been a problem retrieving the item with the specified key.", exc);
		}
	}

	/// <inheritdoc />
	public async ValueTask RemoveAsync(string key)
	{
		try
		{
			await _js.InvokeVoidAsync("localStorage.removeItem", key);
		}
		catch (InvalidOperationException)
		{
			// JS interop is unavailable during static rendering / prerendering; skip silently.
			_logger.WriteDebug(message: "Local storage remove skipped: JavaScript interop is not available during prerendering.");
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { key }))
		{
			throw new UmbrellaBlazorException("There has been a problem removing the item with the specified key.", exc);
		}
	}

	/// <inheritdoc />
	public async ValueTask SetAsync(string key, string value)
	{
		try
		{
			await _js.InvokeVoidAsync("localStorage.setItem", key, value);
		}
		catch (InvalidOperationException)
		{
			// JS interop is unavailable during static rendering / prerendering; skip silently.
			_logger.WriteDebug(message: "Local storage write skipped: JavaScript interop is not available during prerendering.");
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { key }))
		{
			throw new UmbrellaBlazorException("There has been a problem setting the item with the specified key.", exc);
		}
	}
}