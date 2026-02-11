
using CommunityToolkit.Diagnostics;
using CommunityToolkit.Mvvm.Messaging;

namespace Umbrella.AppFramework.Extensions;

/// <summary>
/// Extension methods for the <see cref="IMessenger"/> class.
/// </summary>
public static class IMessengerExtensions
{
	/// <summary>
	/// Tries to register a recipient for a given type of message.
	/// </summary>
	/// <typeparam name="TMessage">The type of message to receive.</typeparam>
	/// <param name="messenger">The <see cref="IMessenger"/> instance to use to register the recipient.</param>
	/// <param name="recipient">The recipient that will receive the messages.</param>
	/// <param name="handler">The <see cref="MessageHandler{TRecipient,TMessage}"/> to invoke when a message is received.</param>
	/// <exception cref="InvalidOperationException">Thrown when trying to register the same message twice.</exception>
	public static void TryRegister<TMessage>(this IMessenger messenger, object recipient, MessageHandler<object, TMessage> handler)
		where TMessage : class
	{
		Guard.IsNotNull(messenger);
		Guard.IsNotNull(recipient);
		Guard.IsNotNull(handler);

		if (messenger.IsRegistered<TMessage>(recipient))
			return;

		messenger.Register(recipient, handler);
	}
}