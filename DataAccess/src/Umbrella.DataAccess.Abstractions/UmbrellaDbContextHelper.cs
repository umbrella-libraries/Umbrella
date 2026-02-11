using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Logging;
using Umbrella.DataAccess.Abstractions.Exceptions;

namespace Umbrella.DataAccess.Abstractions;

/// <summary>
/// A utility used by derived DbContext types to coordinate code execution with repositorites.
/// The lifetime of this utility is registered with the DI container as Scoped to tie it to the lifetime of the DbContext.
/// </summary>
/// <seealso cref="IUmbrellaDbContextHelper" />
public class UmbrellaDbContextHelper : IUmbrellaDbContextHelper
{
	/// <summary>
	/// Gets the log.
	/// </summary>
	protected ILogger Logger { get; }

	/// <summary>
	/// Gets the dictionary containing the pending actions to be executed after <see cref="SaveChanges(Func{int})"/> or <see cref="SaveChangesAsync(Func{CancellationToken, Task{int}}, CancellationToken)"/> is called.
	/// </summary>
	protected Dictionary<object, Func<CancellationToken, Task>> PostSaveChangesSaveActionDictionary { get; } = [];

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaDbContextHelper"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	public UmbrellaDbContextHelper(ILogger<UmbrellaDbContextHelper> logger)
	{
		Logger = logger;
	}

	/// <inheritdoc />
	public virtual void RegisterPostSaveChangesAction(object entity, Func<CancellationToken, Task> wrappedAction)
	{
		try
		{
			PostSaveChangesSaveActionDictionary[entity] = wrappedAction;

			if (Logger.IsEnabled(LogLevel.Debug))
				Logger.WriteDebug(message: "Post save callback registered");
		}
		catch (Exception exc) when (Logger.WriteError(exc))
		{
			throw new UmbrellaDataAccessException("There was a problem registering the action.", exc);
		}
	}

	/// <inheritdoc />
	public virtual async Task ExecutePostSaveChangesActionsAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			if (Logger.IsEnabled(LogLevel.Debug))
				Logger.WriteDebug(new { StartPostSaveChangesActionsCount = PostSaveChangesSaveActionDictionary.Count }, "Started executing post save callbacks");

			// Firstly, create a copy of the callback dictionary and iterate over this
			var dicItem = PostSaveChangesSaveActionDictionary.ToDictionary(x => x.Key, x => x.Value);

			// Now clear the original dictionary so that if any of the callbacks makes a call to SaveChanges we don't end up
			// with infinite recursion.
			PostSaveChangesSaveActionDictionary.Clear();

			// There is the potential that if this code is being executed whilst
			// delegates are still being registered that this will throw up an error.
			// Realistically though I can't see this happening. Not worth building in locking
			// because of the overheads unless we encounter problems.
			foreach (var func in dicItem.Values)
			{
				Task? task = func?.Invoke(cancellationToken);

				if (task is not null)
				{
					if (Logger.IsEnabled(LogLevel.Debug))
						Logger.WriteDebug(message: "Post save callback found to execute");

					await task.ConfigureAwait(false);
				}
			}

			if (Logger.IsEnabled(LogLevel.Debug))
				Logger.WriteDebug(new { EndPostSaveChangesActionsCount = PostSaveChangesSaveActionDictionary.Count }, "Finished executing post save callbacks");
		}
		catch (Exception exc) when (Logger.WriteError(exc))
		{
			throw new UmbrellaDataAccessException("There was a problem executing the pending post-save actions.", exc);
		}
	}

	/// <inheritdoc />
	[SuppressMessage("Usage", "VSTHRD002:Avoid problematic synchronous waits", Justification = "Unavoidable here but this method should never be being called anyway!")]
	public int SaveChanges(Func<int> baseSaveChanges)
	{
		Guard.IsNotNull(baseSaveChanges);

		try
		{
			if (Logger.IsEnabled(LogLevel.Debug))
				Logger.WriteDebug(message: "Started SaveChanges()");

			int result = baseSaveChanges();

			// Run this on a thread pool thread to ensure when this is executed where we have an available
			// SynchronizationContext that it does not cause deadlock
			var t = Task.Run(() => ExecutePostSaveChangesActionsAsync());
			t.Wait();

			if (Logger.IsEnabled(LogLevel.Debug))
				Logger.WriteDebug(message: "Finished SaveChanges()");

			return result;
		}
		catch (Exception exc) when (Logger.WriteError(exc) && !exc.GetType().Name.EndsWith("DbUpdateConcurrencyException", StringComparison.Ordinal))
		{
			throw new UmbrellaDataAccessException("There was a problem saving the changes.", exc);
		}
	}

	/// <inheritdoc />
	public async Task<int> SaveChangesAsync(Func<CancellationToken, Task<int>> baseSaveChangesAsync, CancellationToken cancellationToken = default)
	{
		Guard.IsNotNull(baseSaveChangesAsync);

		try
		{
			if (Logger.IsEnabled(LogLevel.Debug))
				Logger.WriteDebug(message: "Started SaveChangesAsync()");

			int result = await baseSaveChangesAsync(cancellationToken).ConfigureAwait(false);

			await ExecutePostSaveChangesActionsAsync(cancellationToken).ConfigureAwait(false);

			if (Logger.IsEnabled(LogLevel.Debug))
				Logger.WriteDebug(message: "Finished SaveChangesAsync()");

			return result;
		}
		catch (Exception exc) when (Logger.WriteError(exc) && !exc.GetType().Name.EndsWith("DbUpdateConcurrencyException", StringComparison.Ordinal))
		{
			throw new UmbrellaDataAccessException("There was a problem saving the changes.", exc);
		}
	}
}