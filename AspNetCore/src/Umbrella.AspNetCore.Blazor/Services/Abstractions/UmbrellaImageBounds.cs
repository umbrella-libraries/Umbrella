using System.Runtime.InteropServices;

namespace Umbrella.AspNetCore.Blazor.Services.Abstractions;

/// <summary>
/// Represents the displayed bounds of an image in the browser viewport.
/// </summary>
/// <param name="Left">The horizontal position of the left edge.</param>
/// <param name="Top">The vertical position of the top edge.</param>
/// <param name="Width">The displayed width.</param>
/// <param name="Height">The displayed height.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct UmbrellaImageBounds(double Left, double Top, double Width, double Height);
