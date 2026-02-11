


namespace Umbrella.Utilities.Mapping.Mapperly.Enumerations;

/// <summary>
/// The type of the environment that Mapperly is being run in.
/// </summary>
public enum MapperlyEnvironmentType
{
	/// <summary>
	/// Specifies that Mapperly is being used run on a client device, e.g. Blazor, MAUI, etc.
	/// </summary>
	Client,

	/// <summary>
	/// Specifies that Mapperly is being run on the server.
	/// </summary>
	Server
}