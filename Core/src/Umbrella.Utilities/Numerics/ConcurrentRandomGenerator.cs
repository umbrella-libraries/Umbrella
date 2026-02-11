
using System.Buffers;
using System.Security.Cryptography;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Logging;
using Umbrella.Utilities.Exceptions;
using Umbrella.Utilities.Numerics.Abstractions;

namespace Umbrella.Utilities.Numerics;

/// <summary>
/// A thread-safe wrapper around the <see cref="Random"/> type for generating pseudo-random numbers.
/// </summary>
/// <seealso cref="IConcurrentRandomGenerator" />
/// <seealso cref="IDisposable" />
public class ConcurrentRandomGenerator : IConcurrentRandomGenerator, IDisposable
{
	#region Private Members
	private readonly ILogger _log;
	private readonly ThreadLocal<Random> _threadLocalRandom;
	#endregion

	#region Constructors
	/// <summary>
	/// Initializes a new instance of the <see cref="ConcurrentRandomGenerator"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	public ConcurrentRandomGenerator(ILogger<ConcurrentRandomGenerator> logger)
	{
		_log = logger;
		_threadLocalRandom = new ThreadLocal<Random>(CreateRandom);
	}
	#endregion

	#region IConcurrentRandomGenerator Members
	/// <summary>
	/// Returns a non-negative random integer.
	/// </summary>
	/// <returns>A 32-bit signed integer that is greater than or equal to 0 and less than <see cref="int.MaxValue"/>.</returns>
	/// <exception cref="UmbrellaException">An error has occurred while generating the random number.</exception>
	public int GetNext()
	{
		try
		{
			if (_threadLocalRandom.Value is null)
				throw new InvalidOperationException("The value of the thread local is null.");

			return _threadLocalRandom.Value.Next();
		}
		catch (Exception exc) when (_log.WriteError(exc))
		{
			throw new UmbrellaException("An error has occurred while generating the random number.", exc);
		}
	}

	/// <summary>
	/// Gets the next random number based on the specified <paramref name="min"/> and <paramref name="max"/> values.
	/// </summary>
	/// <param name="min">The inclusive lower bound of the random number returned.</param>
	/// <param name="max">The exclusive upper bound of the random number returned. <paramref name="max"/> must be greater than or equal to <paramref name="min"/>.</param>
	/// <returns>
	/// A 32-bit signed integer greater than or equal to <paramref name="min"/> and less than <paramref name="max"/>;
	/// that is, the range of return values includes <paramref name="min"/> but not <paramref name="max"/>. If <paramref name="min"/>
	/// equals <paramref name="max"/>, <paramref name="min"/> is returned.</returns>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if either parameter value is less than zero, or if <paramref name="max"/> is less than <paramref name="min"/>.</exception>
	/// <exception cref="UmbrellaException">An error has occurred while generating the random number.</exception>
	public int GetNext(int min = 0, int max = 0)
	{
		Guard.IsGreaterThanOrEqualTo(min, 0);
		Guard.IsGreaterThanOrEqualTo(max, 0);

		if (min == max)
			return min;

		Guard.IsLessThanOrEqualTo(min, max);

		try
		{
			if (_threadLocalRandom.Value is null)
				throw new InvalidOperationException("The value of the thread local is null.");

			return _threadLocalRandom.Value.Next(min, max);
		}
		catch (Exception exc) when (_log.WriteError(exc, new { min, max }))
		{
			throw new UmbrellaException("An error has occurred while generating the random number.", exc);
		}
	}

	/// <summary>
	/// Generates a unique collection of integers based on the specified <paramref name="min"/> and <paramref name="max"/> values with a size specified by the value of <paramref name="count"/>.
	/// </summary>
	/// <param name="min">The inclusive lower bound of the the random numbers returned.</param>
	/// <param name="max">The exclusive upper bound of the random numbers returned. <paramref name="max"/> must be greater than or equal to <paramref name="min"/>.</param>
	/// <param name="count">The size of the generated collection.</param>
	/// <param name="shuffle">Determines whether or not the returned collection will be randomly shuffled.</param>
	/// <returns>A collection containing the random numbers.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown if either parameter value is less than zero, or if <paramref name="max"/> is less than <paramref name="min"/>.
	/// Also conditionally thrown, when <paramref name="max"/> is greater than <paramref name="min"/>, if the <paramref name="count"/>
	/// is less than one or is greater than the difference between <paramref name="min"/> and <paramref name="max"/> such that it will be
	/// impossible to generate a collection of the size of the specified <paramref name="count"/> value.
	/// </exception>
	/// <exception cref="UmbrellaException">An error has occurred while generating the collection.</exception>
	public IReadOnlyCollection<int> GenerateDistinctCollection(int min, int max, int count, bool shuffle = false)
	{
		Guard.IsGreaterThanOrEqualTo(min, 0);
		Guard.IsGreaterThanOrEqualTo(max, 0);

		if (min == max)
			return new int[] { min };

		Guard.IsLessThanOrEqualTo(min, max, nameof(min));
		Guard.IsBetweenOrEqualTo(count, 1, max - min, nameof(count));

		try
		{
			// Using a HashSet to ensure a unique collection
			var candidates = new HashSet<int>();

			while (candidates.Count < count)
			{
				_ = candidates.Add(GetNext(min, max));
			}

			if (!shuffle)
				return candidates;

			// Load them in to an array for shuffling
			int[] results = candidates.ToArray();

			// Shuffle the results
			int i = results.Length;

			while (i > 1)
			{
				int k = GetNext(max: i--);
				(results[i], results[k]) = (results[k], results[i]);
			}

			return results;
		}
		catch (Exception exc) when (_log.WriteError(exc, new { min, max, count, shuffle }))
		{
			throw new UmbrellaException("An error has occurred while generating the collection.", exc);
		}
	}

	/// <summary>
	/// Returns a specified number of random elements from a sequence.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <param name="source">The sequence to return elements from.</param>
	/// <param name="count">The number of elements to return.</param>
	/// <param name="shuffle">Determines whether or not the returned sequence will be randomly shuffled.</param>
	/// <returns>An <see cref="IEnumerable{T}"/> that contains the specified number of elements randomly selected from the sequence.</returns>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="source"/> is null.</exception>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="count"/> is less than one.</exception>
	public IEnumerable<T> TakeRandom<T>(IEnumerable<T> source, int count, bool shuffle = false)
	{
		Guard.IsNotNull(source);

		int sourceCount = source.Count();

		if (sourceCount is 0)
			yield break;

		Guard.IsBetweenOrEqualTo(count, 1, sourceCount, nameof(count));

		var indexes = GenerateDistinctCollection(0, sourceCount, count, shuffle);

		foreach (int idx in indexes)
		{
			yield return source.ElementAt(idx);
		}
	}
	#endregion

	#region Private Methods
	private Random CreateRandom()
	{
		byte[]? buffer = null;

		try
		{
			buffer = ArrayPool<byte>.Shared.Rent(4);

			using var rng = RandomNumberGenerator.Create();
			rng.GetBytes(buffer, 0, 4);

			int seed = BitConverter.ToInt32(buffer, 0);

			return new Random(seed);
		}
		finally
		{
			if (buffer is not null)
				ArrayPool<byte>.Shared.Return(buffer);
		}
	}
	#endregion

	#region IDisposable Support
	private bool _isDisposed;

	/// <summary>
	/// Releases unmanaged and - optionally - managed resources.
	/// </summary>
	/// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
	protected virtual void Dispose(bool disposing)
	{
		if (!_isDisposed)
		{
			if (disposing)
			{
				_threadLocalRandom.Dispose();
			}

			_isDisposed = true;
		}
	}

	/// <summary>
	/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
	/// </summary>
	public void Dispose()
	{
		try
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
			GC.SuppressFinalize(this);
		}
		catch (Exception exc) when (_log.WriteError(exc))
		{
			throw;
		}
	}
	#endregion
}