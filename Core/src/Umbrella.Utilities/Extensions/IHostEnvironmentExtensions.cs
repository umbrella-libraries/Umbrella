


#pragma warning disable IDE0130
namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Extension methods for <see cref="IHostEnvironment"/>.
/// </summary>
public static class IHostEnvironmentExtensions
{
	/// <summary>
	/// Checks if the current host environment name is <c>QA</c>
	/// </summary>
	/// <param name="hostEnvironment">The host environment.</param>
	/// <returns>
	///   <see langword="true"/> if the specified host environment is <c>QA</c>; otherwise, <see langword="false"/>.
	/// </returns>
	public static bool IsQA(this IHostEnvironment hostEnvironment) => hostEnvironment.IsEnvironment("QA");

	/// <summary>
	/// Checks if the current host environment name is <c>Hotfix</c>
	/// </summary>
	/// <param name="hostEnvironment">The host environment.</param>
	/// <returns>
	///   <see langword="true"/> if the specified host environment is <c>Hotfix</c>; otherwise, <see langword="false"/>.
	/// </returns>
	public static bool IsHotfix(this IHostEnvironment hostEnvironment) => hostEnvironment.IsEnvironment("Hotfix");

	/// <summary>
	/// Checks if the current host environment name is <c>Integration</c>
	/// </summary>
	/// <param name="hostEnvironment">The host environment.</param>
	/// <returns>
	///   <see langword="true"/> if the specified host environment is <c>Integration</c>; otherwise, <see langword="false"/>.
	/// </returns>
	public static bool IsIntegration(this IHostEnvironment hostEnvironment) => hostEnvironment.IsEnvironment("Integration");

	/// <summary>
	/// Checks if the current host environment name is <c>PreProduction</c>
	/// </summary>
	/// <param name="hostEnvironment">The host environment.</param>
	/// <returns>
	///   <see langword="true"/> if the specified host environment is <c>PreProduction</c>; otherwise, <see langword="false"/>.
	/// </returns>
	public static bool IsPreProduction(this IHostEnvironment hostEnvironment) => hostEnvironment.IsEnvironment("PreProduction");
}