


namespace Umbrella.Legacy.WebUtilities.Extensions;

/// <summary>
/// Extensions methods for use with the <see cref="AppDomain"/> type.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Extension methods.")]
public static class AppDomainExtensions
{
	private static readonly bool _isOwinApplication;

	/// <summary>
	/// Initializes the <see cref="AppDomainExtensions"/> class.
	/// </summary>
	static AppDomainExtensions()
	{
		_isOwinApplication = AppDomain.CurrentDomain.GetAssemblies().Any(x => x.FullName.Contains("Owin"));
	}

	/// <summary>
	/// Determines whether the current web application is an OWIN App.
	/// </summary>
	/// <param name="appDomain">The application domain.</param>
	/// <returns><see langword="true"/> if it is an OWIN App; otherwise <see langword="false"/>.</returns>
	public static bool IsOwinApp(this AppDomain appDomain) => _isOwinApplication;
}