


namespace Umbrella.Utilities.Networking.Abstractions;

/// <summary>
/// A utility used to determine if there is a network connection.
/// </summary>
public interface INetworkConnectionStatusUtility
{
	/// <summary>
	/// Returns <see langword="true" /> if there is a network connection; otherwise <see langword="false"/>.
	/// </summary>
	bool IsConnected { get; }
}