


using System.Buffers;
using System.Security.Cryptography;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Logging;
using Umbrella.Utilities.Encryption.Abstractions;
using Umbrella.Utilities.Exceptions;

namespace Umbrella.Utilities.Encryption;

/// <summary>
/// Generates cryptographically strong nonces.
/// </summary>
/// <seealso cref="INonceGenerator" />
/// <seealso cref="IDisposable" />
public class NonceGenerator : INonceGenerator
{
	private readonly ILogger _log;

	/// <summary>
	/// Initializes a new instance of the <see cref="NonceGenerator"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	public NonceGenerator(ILogger<NonceGenerator> logger)
	{
		_log = logger;
	}

	/// <summary>
	/// Generates a nonce encoded as a base64 string according to the specified length in bytes.
	/// </summary>
	/// <param name="lengthInBytes">The length in bytes.</param>
	/// <returns>A base64 encoded string.</returns>
	/// <exception cref="UmbrellaException">An error has occurred whilst generating the nonce of {lengthInBytes}</exception>
	public string Generate(int lengthInBytes)
	{
		Guard.IsBetweenOrEqualTo(lengthInBytes, 1, 1024, nameof(lengthInBytes));

		byte[]? buffer = null;

		try
		{
			buffer = ArrayPool<byte>.Shared.Rent(lengthInBytes);

			using var rng = RandomNumberGenerator.Create();
			rng.GetBytes(buffer, 0, lengthInBytes);

			return Convert.ToBase64String(buffer, 0, lengthInBytes);
		}
		catch (Exception exc) when (_log.WriteError(exc, new { lengthInBytes }))
		{
			throw new UmbrellaException($"An error has occurred whilst generating the nonce of {lengthInBytes}.", exc);
		}
		finally
		{
			if (buffer is not null)
				ArrayPool<byte>.Shared.Return(buffer);
		}
	}
}