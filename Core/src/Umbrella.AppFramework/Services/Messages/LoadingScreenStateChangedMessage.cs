
using CommunityToolkit.Mvvm.Messaging.Messages;
using Umbrella.AppFramework.Services.Enumerations;

namespace Umbrella.AppFramework.Services.Messages;

/// <summary>
/// A message that signals when the <see cref="LoadingScreenState"/> has changed.
/// </summary>
/// <seealso cref="ValueChangedMessage{LoadingScreenState}" />
public class LoadingScreenStateChangedMessage : ValueChangedMessage<LoadingScreenState>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="LoadingScreenStateChangedMessage"/> class.
	/// </summary>
	/// <param name="value">The value that has changed.</param>
	public LoadingScreenStateChangedMessage(LoadingScreenState value) : base(value)
	{
	}
}