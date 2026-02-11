


using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Globalization;
using System.Security.Cryptography;
using Umbrella.Utilities.Constants;
using Umbrella.Utilities.Encryption.Abstractions;
using Umbrella.Utilities.Encryption.Options;
using Umbrella.Utilities.Exceptions;

namespace Umbrella.Utilities.Encryption;

/// <summary>
/// A utility to generate random strings using the default cryptographically strong <see cref="RandomNumberGenerator"/> class internally.
/// </summary>
/// <seealso cref="ISecureRandomStringGenerator" />
/// <seealso cref="IDisposable" />
public class SecureRandomStringGenerator : ISecureRandomStringGenerator
{
	#region Private Static Members
	private static readonly char[] _lowerCaseLettersArray = ['a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z'];
	private static readonly char[] _upperCaseLettersArray = _lowerCaseLettersArray.Select(char.ToUpperInvariant).ToArray();
	#endregion

	#region Private Members
	private readonly ILogger _log;
	private readonly SecureRandomStringGeneratorOptions _options;
	#endregion

	#region Constructors		
	/// <summary>
	/// Initializes a new instance of the <see cref="SecureRandomStringGenerator"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="options">The options.</param>
	/// <exception cref="UmbrellaException">There has been a problem creating this instance using the specified options.</exception>
	public SecureRandomStringGenerator(
		ILogger<SecureRandomStringGenerator> logger,
		SecureRandomStringGeneratorOptions options)
	{
		_log = logger;
		_options = options;
	}
	#endregion

	#region IPasswordGenerator Members
	/// <summary>
	/// Generates a random string of the specified length using the specified options.
	/// If the sum of <paramref name="numbers"/>, <paramref name="upperCaseCharacters"/> and <paramref name="specialCharacters"/>
	/// is less than the <paramref name="length"/>, the remaining characters of the generated string will be filled using lowercase characters.
	/// </summary>
	/// <param name="length">The length.</param>
	/// <param name="numbers">The number of numbers.</param>
	/// <param name="upperCaseCharacters">The number of upper case letters.</param>
	/// <param name="specialCharacters">The number of special characters.</param>
	/// <returns>The generated string.</returns>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="length"/> is less than 1.</exception>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="numbers"/> is less than 0.</exception>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="upperCaseCharacters"/> is less than 0.</exception>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="specialCharacters"/> is less than 0.</exception>
	/// <exception cref="UmbrellaException">There has been a problem generating the random string.</exception>
	public string Generate(int length = 8, int numbers = 0, int upperCaseCharacters = 0, int specialCharacters = 0)
	{
		Guard.IsGreaterThanOrEqualTo(length, 1, nameof(length));
		Guard.IsGreaterThanOrEqualTo(numbers, 0, nameof(numbers));
		Guard.IsGreaterThanOrEqualTo(upperCaseCharacters, 0, nameof(upperCaseCharacters));
		Guard.IsGreaterThanOrEqualTo(specialCharacters, 0, nameof(specialCharacters));

		int nonLowerCaseLength = numbers + upperCaseCharacters + specialCharacters;

		Guard.IsLessThanOrEqualTo(nonLowerCaseLength, length, nameof(length));

		try
		{
			Span<char> randomString = length <= StackAllocConstants.MaxCharSize ? stackalloc char[length] : new char[length];

			// We are building up a string here starting with lowercase letters, followed by uppercase and finally numbers.
			int idx = 0;

			// Numbers
			while (idx < numbers)
			{
				int number = GenerateRandomInteger(0, 10);

				randomString[idx++] = number.ToString(CultureInfo.InvariantCulture)[0];
			}

			// Uppercase
			while (idx < upperCaseCharacters + numbers)
			{
				int index = GenerateRandomInteger(0, 26);
				char letter = _upperCaseLettersArray[index];

				randomString[idx++] = letter;
			}

			// Special Characters
			while (idx < upperCaseCharacters + numbers + specialCharacters)
			{
				int index = GenerateRandomInteger(0, _options.SpecialCharacters.Count);
				char letter = _options.SpecialCharacters[index];

				randomString[idx++] = letter;
			}

			// Lowercase
			while (idx < length)
			{
				int index = GenerateRandomInteger(0, 26);
				char letter = _lowerCaseLettersArray[index];

				randomString[idx++] = letter;
			}

			// Randomly shuffle the generated string
			int n = randomString.Length;
			while (n > 1)
			{
				int k = GenerateRandomInteger(0, n--);
				(randomString[k], randomString[n]) = (randomString[n], randomString[k]);
			}

			return randomString.ToString();
		}
		catch (Exception exc) when (_log.WriteError(exc, new { length, numbers, upperCaseCharacters, specialCharacters }))
		{
			throw new UmbrellaException("There has been a problem generating the random string.", exc);
		}
	}

#if !AzureDevOps
	[Obsolete]
	[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Advanced)]
	internal string GenerateOld(int length = 8, int numbers = 0, int upperCaseCharacters = 0, int specialCharacters = 0)
	{
		try
		{
			if (length < 1)
				throw new ArgumentOutOfRangeException(nameof(length), "Must be greater than or equal to 1.");

			if (numbers < 0)
				throw new ArgumentOutOfRangeException(nameof(numbers), "Must be greater than or equal to 0.");

			if (numbers > length)
				throw new ArgumentOutOfRangeException(nameof(numbers), "Must be less than or equal to length.");

			if (upperCaseCharacters < 0)
				throw new ArgumentOutOfRangeException(nameof(upperCaseCharacters), "Must be greater than or equal to 0.");

			if (upperCaseCharacters > length)
				throw new ArgumentOutOfRangeException(nameof(upperCaseCharacters), "Must be less than or equal to length.");

			if (numbers + upperCaseCharacters > length)
				throw new ArgumentOutOfRangeException($"{nameof(numbers)}, {nameof(upperCaseCharacters)}", $"The sum of the {nameof(numbers)} and the {nameof(upperCaseCharacters)} arguments is greater than the length.");

			char[] password = new char[length];

			int idx = 0;

			// Numbers
			while (idx < numbers)
			{
				int number = GenerateRandomInteger(0, 10);

				password[idx++] = number.ToString(CultureInfo.InvariantCulture)[0];
			}

			// Uppercase
			while (idx < upperCaseCharacters + numbers)
			{
				int index = GenerateRandomInteger(0, 26);
				char letter = _upperCaseLettersArray[index];

				password[idx++] = letter;
			}

			// Special Characters
			while (idx < upperCaseCharacters + numbers + specialCharacters)
			{
				int index = GenerateRandomInteger(0, _options.SpecialCharacters.Count);
				char letter = _options.SpecialCharacters[index];

				password[idx++] = letter;
			}

			// Lowercase
			while (idx < length)
			{
				int index = GenerateRandomInteger(0, 26);
				char letter = _lowerCaseLettersArray[index];

				password[idx++] = letter;
			}

			// Randomly shuffle the generated string
			int n = password.Length;
			while (n > 1)
			{
				int k = GenerateRandomInteger(0, n--);
				(password[k], password[n]) = (password[n], password[k]);
			}

			return new string(password);
		}
		catch (Exception exc) when (_log.WriteError(exc, new { length, numbers }))
		{
			throw;
		}
	}
#endif
	#endregion

	#region Private Members
	private static int GenerateRandomInteger(int min, int max)
	{
		if (min == max)
			return min;

		uint scale = uint.MaxValue;

		while (scale == uint.MaxValue)
		{
			byte[]? buffer = null;

			try
			{
				buffer = ArrayPool<byte>.Shared.Rent(4);

				using var rng = RandomNumberGenerator.Create();
				rng.GetBytes(buffer, 0, 4);

				// Convert that into an uint.
				scale = BitConverter.ToUInt32(buffer, 0);
			}
			finally
			{
				if (buffer is not null)
					ArrayPool<byte>.Shared.Return(buffer);
			}
		}

		// Add min to the scaled difference between max and min.
		return (int)(min + ((max - min) *
			(scale / (double)uint.MaxValue)));
	}
	#endregion
}