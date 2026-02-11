


using Microsoft.Maui.Networking;
using Umbrella.Utilities.Networking.Abstractions;

namespace Umbrella.Maui.Networking;

/// <summary>
/// A utility used to determine if there is a network connection.
/// </summary>
/// <seealso cref="INetworkConnectionStatusUtility" />
public class NetworkConnectionStatusUtility : INetworkConnectionStatusUtility
{
	// TODO: Can we make this a little bit more sophisticated by pinging an endpoint when the NetworkAccess == NetworkAccess.Internet.
	// Maybe ping every x seconds and if no response set IsConnected to false.

	/// <inheritdoc />
	public bool IsConnected => Connectivity.NetworkAccess == NetworkAccess.Internet;
}