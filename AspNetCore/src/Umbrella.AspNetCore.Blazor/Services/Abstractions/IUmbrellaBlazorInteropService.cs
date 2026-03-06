
namespace Umbrella.AspNetCore.Blazor.Services.Abstractions;

/// <summary>
/// A utility containing core interop functionality between Blazor and JavaScript for features not yet supported
/// natively by Blazor.
/// </summary>
public interface IUmbrellaBlazorInteropService
{
	/// <summary>
	/// An event that is raised when the browser window is scrolled to the top.
	/// </summary>
	event AwaitableBlazorEventHandler OnWindowScrolledTop;

	/// <summary>
	/// Animates the scroll position of the browser window to the specified position.
	/// </summary>
	/// <param name="scrollY">The vertical position.</param>
	/// <param name="offset">The vertical offset.</param>
	/// <returns>A <see cref="ValueTask"/> that completes when the operation has completed.</returns>
	ValueTask ScrollToAsync(int scrollY, int offset = 0);

	/// <summary>
	/// Animates the scroll position of the browser window to the specified element.
	/// </summary>
	/// <param name="elementSelector">The element selector specified using CSS selector syntax.</param>
	/// <param name="offset">The vertical offset.</param>
	/// <returns>A <see cref="ValueTask"/> that completes when the operation has completed.</returns>
	ValueTask ScrollToAsync(string elementSelector, int offset = 0);

	/// <summary>
	/// Animates the scroll position of the browser window to the bottom of the document.
	/// </summary>
	/// <returns>A <see cref="ValueTask"/> that completes when the operation has completed.</returns>
	ValueTask ScrollToBottomAsync();

	/// <summary>
	/// Triggers a click event on the first element matching the specified selector.
	/// </summary>
	/// <param name="elementSelector">The element selector specified using CSS selector syntax.</param>
	/// <returns>A <see cref="ValueTask"/> that completes when the operation has completed.</returns>
	ValueTask TriggerElementClickAsync(string elementSelector);

	/// <summary>
	/// Opens the specified <paramref name="url"/> using the specified <paramref name="target"/>.
	/// </summary>
	/// <param name="url">The URL.</param>
	/// <param name="target">The target.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A <see cref="ValueTask"/> that completes when the operation has completed.</returns>
	ValueTask OpenUrlAsync(string url, string target, CancellationToken cancellationToken = default);

	/// <summary>
	/// Sets the value for the <![CDATA[<title>]]> tag in the HTML <![CDATA[<head>]]> element.
	/// </summary>
	/// <param name="pageTitle">The page title.</param>
	/// <returns>A <see cref="ValueTask"/> that completes when the operation has completed.</returns>
	ValueTask SetPageTitleAsync(string pageTitle);
}