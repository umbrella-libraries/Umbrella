


using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Umbrella.AppFramework.Services.Messages;

/// <summary>
/// A message for use with implementations of <see cref="IMessenger"/> which is used to send a notification to subscribers
/// when an update is available for the current version of the target application.
/// </summary>
/// <remarks>
/// This is primarily for use with installed client applications, e.g. Xamarin, where a newer version of the client application
/// has been made available for the user to download and the user needs to be notified.
/// </remarks>
public class AppUpdateStateChangedMessage : ValueChangedMessage<(bool updateRequired, string message)>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="AppUpdateStateChangedMessage"/> class.
	/// </summary>
	/// <param name="value">The value that has changed.</param>
	public AppUpdateStateChangedMessage((bool updateRequired, string message) value) : base(value)
	{
	}
}