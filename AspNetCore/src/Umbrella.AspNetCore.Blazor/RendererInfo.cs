#if NET8_0
namespace Umbrella.AspNetCore.Blazor;

// Temporary net8.0 shim for ComponentBase.RendererInfo (.NET 9+).
// Only valid for WebAssembly consumers - reports non-interactive on Server and Hybrid renderers.
// Remove together with the net8.0 target when .NET 8 support is dropped.
internal static class RendererInfo
{
	public static bool IsInteractive => OperatingSystem.IsBrowser();
}
#endif
