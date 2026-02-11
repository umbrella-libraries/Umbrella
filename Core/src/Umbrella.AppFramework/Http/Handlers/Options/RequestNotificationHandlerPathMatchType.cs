


namespace Umbrella.AppFramework.Http.Handlers.Options;

/// <summary>
/// The type used when matching paths.
/// </summary>
public enum RequestNotificationHandlerPathMatchType
{
	/// <summary>
	/// The paths must exactly match.
	/// </summary>
	Exact,

	/// <summary>
	/// The request path must start with the specified path.
	/// </summary>
	StartsWith
}