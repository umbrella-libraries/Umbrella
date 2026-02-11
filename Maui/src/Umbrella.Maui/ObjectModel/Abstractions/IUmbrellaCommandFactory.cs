


using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace Umbrella.Maui.ObjectModel.Abstractions;

/// <summary>
/// A factory used to create instances of <see cref="ICommand"/> for use with views and view models in .NET MAUI.
/// </summary>
public interface IUmbrellaCommandFactory
{
	AsyncRelayCommand CreateAsyncCommand(Func<Task> execute, bool checkNetworkConnection = false, Func<bool>? canExecute = null);
	AsyncRelayCommand<T> CreateAsyncCommand<T>(Func<T, Task> execute, bool checkNetworkConnection = false, Func<bool>? canExecute = null);
	RelayCommand CreateCommand(Action execute, Func<bool>? canExecute = null);
	RelayCommand<T> CreateCommand<T>(Action<T?> execute, Predicate<T?>? canExecute = null);
}