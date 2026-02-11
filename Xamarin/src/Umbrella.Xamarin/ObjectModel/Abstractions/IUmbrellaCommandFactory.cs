
using System.Windows.Input;
using Xamarin.CommunityToolkit.ObjectModel;
using Xamarin.Forms;

namespace Umbrella.Xamarin.ObjectModel.Abstractions;

/// <summary>
/// A factory used to create instances of <see cref="ICommand"/> for use with views and view models in Xamarin.
/// </summary>
public interface IUmbrellaCommandFactory
{
	/// <summary>
	/// Creates an <see cref="AsyncCommand"/> with the specified delegate.
	/// </summary>
	/// <param name="execute">The delegate which is executed by the command.</param>
	/// <param name="checkNetworkConnection">Determines whether the command should check for an active network connection before executing.</param>
	/// <param name="canExecute">The delegate which determines whether the command can be executed.</param>
	/// <returns>The command.</returns>
	AsyncCommand CreateAsyncCommand(Func<Task> execute, bool checkNetworkConnection = false, Func<bool>? canExecute = null);

	/// <summary>
	/// Creates an <see cref="AsyncCommand{T}"/> with the specified delegate.
	/// </summary>
	/// <typeparam name="T">The type of the argument passed as a parameter to the <paramref name="execute"/> delegate.</typeparam>
	/// <param name="execute">The delegate which is executed by the command.</param>
	/// <param name="checkNetworkConnection">iDetermines whether the command should check for an active network connection before executing.</param>
	/// <param name="canExecute">The delegate which determines whether the command can be executed.</param>
	/// <returns>The command.</returns>
	AsyncCommand<T> CreateAsyncCommand<T>(Func<T, Task> execute, bool checkNetworkConnection = false, Func<bool>? canExecute = null);

	/// <summary>
	/// Creates an <see cref="AsyncValueCommand"/> with the specified delegate.
	/// </summary>
	/// <param name="execute">The delegate which is executed by the command.</param>
	/// <param name="checkNetworkConnection">Determines whether the command should check for an active network connection before executing.</param>
	/// <param name="canExecute">The delegate which determines whether the command can be executed.</param>
	/// <returns>The command.</returns>
	AsyncValueCommand CreateAsyncValueCommand(Func<ValueTask> execute, bool checkNetworkConnection = false, Func<bool>? canExecute = null);

	/// <summary>
	/// Creates an <see cref="AsyncValueCommand{T}"/> with the specified delegate.
	/// </summary>
	/// <typeparam name="T">The type of the argument passed as a parameter to the <paramref name="execute"/> delegate.</typeparam>
	/// <param name="execute">The delegate which is executed by the command.</param>
	/// <param name="checkNetworkConnection">iDetermines whether the command should check for an active network connection before executing.</param>
	/// <param name="canExecute">The delegate which determines whether the command can be executed.</param>
	/// <returns>The command.</returns>
	AsyncValueCommand<T> CreateAsyncValueCommand<T>(Func<T, ValueTask> execute, bool checkNetworkConnection = false, Func<bool>? canExecute = null);

	/// <summary>
	/// Creates an <see cref="Command"/> with the specified delegate.
	/// </summary>
	/// <param name="execute">The delegate which is executed by the command.</param>
	/// <param name="canExecute">The delegate which determines whether the command can be executed.</param>
	/// <returns>The command.</returns>
	Command CreateCommand(Action execute, Func<bool>? canExecute = null);

	/// <summary>
	/// Creates an <see cref="Command{T}"/> with the specified delegate.
	/// </summary>
	/// <typeparam name="T">The type of the argument passed as a parameter to the <paramref name="execute"/> delegate.</typeparam>
	/// <param name="execute">The delegate which is executed by the command.</param>
	/// <param name="canExecute">The delegate which determines whether the command can be executed.</param>
	/// <returns>The command.</returns>
	Command<T> CreateCommand<T>(Action<T> execute, Func<T, bool>? canExecute = null);
}