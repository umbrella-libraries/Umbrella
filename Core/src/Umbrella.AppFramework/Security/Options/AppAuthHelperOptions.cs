


namespace Umbrella.AppFramework.Security.Options;

/// <summary>
/// Options for use with the <see cref="JwtAppAuthHelper"/> class.
/// </summary>
public class AppAuthHelperOptions
{
	/// <summary>
	/// Gets the post logout action.
	/// </summary>
	public Func<ValueTask>? PostLogoutAction { get; set; }
}