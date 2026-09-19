using System.Diagnostics;

namespace Umbrella.Utilities.Compilation;

/// <summary>
/// This is an internal class used only for the purposes of debugging the library projects. Exposing this for use outside
/// of these projects would be pointless once the libraries have been compiled in release mode.
/// </summary>
internal static class DebugUtility
{
	public static bool IsDebug
	{
		get
		{
			bool isDebugMode = false;

			IAmDebug(ref isDebugMode);

			return isDebugMode;
		}
	}

	public static string BuildConfiguration
	{
		get
		{
			string configuration = "release";

#if DEBUG
			configuration = "debug";
#endif

			return configuration;
		}
	}

	[Conditional("DEBUG")]
	private static void IAmDebug(ref bool isDebugMode) => isDebugMode = true;
}
