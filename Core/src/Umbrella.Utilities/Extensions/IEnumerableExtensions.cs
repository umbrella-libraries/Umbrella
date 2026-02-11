


using System.Collections.Concurrent;
using CommunityToolkit.Diagnostics;

namespace Umbrella.Utilities.Extensions;

/// <summary>
/// Extension methods for types that implement <see cref="IEnumerable{T}"/>.
/// </summary>
public static class IEnumerableExtensions
{
	/// <summary>
	/// Splits a collection into groups
	/// </summary>
	/// <typeparam name="T">The type of the source collection</typeparam>
	/// <param name="source">The collection to be split into groups</param>
	/// <param name="itemsPerGroup">The number of items per group</param>
	/// <returns>A nested collection split into groups</returns>
	[Obsolete("This will be removed in a future version. Please use the new .NET 6 Chunk method when available.")]
	public static IEnumerable<IEnumerable<T>> Split<T>(this IEnumerable<T> source, int itemsPerGroup)
	{
		Guard.IsNotNull(source);
		Guard.IsGreaterThanOrEqualTo(itemsPerGroup, 1);

		return source
			.Select((x, i) => new { Index = i, Value = x })
			.GroupBy(x => x.Index / itemsPerGroup)
			.Select(x => x.Select(v => v.Value));
	}

	/// <summary>
	/// Converts the source <see cref="IEnumerable{T}"/> to a <see cref="HashSet{T}"/>.
	/// </summary>
	/// <typeparam name="T">The type of the items in the collections.</typeparam>
	/// <param name="source">The source.</param>
	/// <param name="comparer">The comparer used by the hashset to determine item uniqueness.</param>
	/// <returns>The new <see cref="HashSet{T}"/>.</returns>
	[Obsolete("This will be removed in a future version as this method has been added to netstandard2.1")]
	public static HashSet<T> ToHashSet<T>(this IEnumerable<T> source, IEqualityComparer<T>? comparer = null)
	{
		Guard.IsNotNull(source);

		return new HashSet<T>(source, comparer);
	}

	/// <summary>
	/// Performs the specified <paramref name="action"/> on each element of <paramref name="source"/>.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <param name="source">A sequence of values to invoke the <paramref name="source"/> function on.</param>
	/// <param name="action">The action to perform on each element of <paramref name="source"/>.</param>
	/// <returns>An <see cref="IEnumerable{T}" /> whose elements are the result of invoking the transform function on each element of source.</returns>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="source"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="action"/> is null.</exception>
	public static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
	{
		Guard.IsNotNull(source);
		Guard.IsNotNull(action);

		foreach (var item in source)
		{
			action(item);
		}
	}

	/// <summary>
	/// Projects each element of a sequence into a new form asynchronously, either in parallel or sequentially depending on the <paramref name="ensureSequentialExecution"/> parameter.
	/// </summary>
	/// <typeparam name="TSource">The type of the elements of source.</typeparam>
	/// <typeparam name="TResult">The type of the value returned by selector.</typeparam>
	/// <param name="source">A sequence of values to invoke a transform function on.</param>
	/// <param name="selector">A transform function to apply to each element.</param>
	/// <param name="ensureSequentialExecution">Ensures that each <paramref name="selector"/> is executed sequentially.</param>
	/// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
	/// <returns>
	/// An awaitable Task whose result contains an <see cref="IEnumerable{TResult}"/> whose elements are the result of 
	/// invoking the transform function on each element of <paramref name="source"/>.
	/// </returns>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="source"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="selector"/> is null.</exception>
	public static async Task<IEnumerable<TResult>> SelectAsync<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, CancellationToken, Task<TResult>> selector, bool ensureSequentialExecution = false, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(source);
		Guard.IsNotNull(selector);

		if (ensureSequentialExecution)
		{
			List<TResult> lstResult = [];

			foreach(var item in source)
			{
				lstResult.Add(await selector(item, cancellationToken).ConfigureAwait(false));
			}

			return lstResult;
		}

		return await Task.WhenAll(source.Select(x => selector(x, cancellationToken))).ConfigureAwait(false);
	}

	/// <summary>
	/// Allows parallel processing of asynchronous code on a specified collection.
	/// </summary>
	/// <typeparam name="T">The type of the item in the collection.</typeparam>
	/// <param name="source">The source.</param>
	/// <param name="funcBody">The function body.</param>
	/// <param name="degreeOfParallelism">The degree of parallelism.</param>
	/// <returns>An awaitabe <see cref="Task"/>.</returns>
	public static Task ParallelForEachAsync<T>(this IEnumerable<T> source, Func<T, Task> funcBody, int degreeOfParallelism = 4)
	{
		async Task AwaitPartitionAsync(IEnumerator<T> partition)
		{
			using (partition)
			{
				while (partition.MoveNext())
				{
					await Task.Yield(); // prevents a sync/hot thread hangup
					await funcBody(partition.Current).ConfigureAwait(false);
				}
			}
		}

		return Task.WhenAll(
			Partitioner
				.Create(source)
				.GetPartitions(degreeOfParallelism)
				.AsParallel()
				.Select(AwaitPartitionAsync));
	}

	/// <summary>
	/// Parses the string in the <paramref name="items"/> to integers. Any items that cannot be parsed are discarded.
	/// </summary>
	/// <param name="items">The items.</param>
	/// <returns>The parsed integers.</returns>
	public static IEnumerable<int> ParseIntegers(this IEnumerable<string> items)
	{
		Guard.IsNotNull(items);

		foreach (string item in items)
		{
			if (int.TryParse(item, out int value))
				yield return value;
		}
	}

	/// <summary>
	/// Randomly shuffles the specified items.
	/// </summary>
	/// <typeparam name="T">The type of the item being shuffed.</typeparam>
	/// <param name="items">The items.</param>
	/// <returns>The shuffled items.</returns>
	public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> items) => items.OrderBy(x => Guid.NewGuid());
}