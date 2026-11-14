# Viewports helper

Put this in the browser test project's shared folder, for example `Shared\Viewports.cs`, with the widths found in Preflight step 8. One width per band of widths the layout changes at; the names are the baselines' second segment (`track-listing-page.phone.windows.png`).

```csharp
using Microsoft.Playwright;

/// <summary>
/// The browser widths the visual and accessibility tests run at: one in each band of widths the site's layout changes
/// at.
/// </summary>
/// <remarks>
/// <para>
/// The styles use Bootstrap's default breakpoints through <c>media-breakpoint-up(md|lg|xl)</c> (768, 992 and 1200px),
/// and the scripts switch at 768px (the carousels) and 1200px (the menu, which is the phone menu below it). Containers
/// stop growing at 1200px, so wider windows only add margin.
/// </para>
/// <para>
/// A test sets its width before opening the page, and names its baselines <c>{name}.{viewport}</c>.
/// </para>
/// </remarks>
internal static class Viewports
{
	public const string Phone = "phone";
	public const string Tablet = "tablet";
	public const string Laptop = "laptop";
	public const string Desktop = "desktop";

	private const int Height = 800;

	private static readonly Dictionary<string, int> _widths = new(StringComparer.Ordinal)
	{
		[Phone] = 375,
		[Tablet] = 768,
		// Below the menu's 1200px, so it shows the phone menu.
		[Laptop] = 992,
		[Desktop] = 1280,
	};

	/// <summary>Gets every width, for the visual baselines.</summary>
	public static IReadOnlyCollection<string> All { get; } = [.. _widths.Keys];

	/// <summary>
	/// Gets the narrowest and widest layouts, for the accessibility baselines. Axe checks the DOM and styles, which change
	/// most between the phone layout (the phone menu, stacked columns) and the desktop one.
	/// </summary>
	public static IReadOnlyCollection<string> Accessibility { get; } = [Phone, Desktop];

	public static int WidthOf(string viewport) => _widths[viewport];

	public static Task SetAsync(IPage page, string viewport) => page.SetViewportSizeAsync(WidthOf(viewport), Height);
}
```

Notes:

- Write in the remarks where each width comes from (the stylesheet breakpoints and the script switches), so a later change to the breakpoints shows which widths to revisit.
- Add a wider width (for example `wide` at 1440) only when something changes above the desktop width: a container that keeps growing, an `xxl` rule, a hero or footer that switches layout.
- Keep the base class's default viewport (1280 by 800) for tests that don't depend on width; journey tests usually do not need every width.
- Set the width before `GotoAsync`: pages that choose a layout, image sources or a carousel's slide count at load time keep what they chose when the window is resized afterwards.
