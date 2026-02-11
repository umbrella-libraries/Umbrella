
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Logging;
using Umbrella.Utilities.Data.Abstractions;
using Umbrella.Utilities.Exceptions;
using Umbrella.Utilities.Extensions;

namespace Umbrella.Utilities.Data;

/// <summary>
/// A utility used to normalize string values and then converting them to uppercase using the rules of the invariant culture.
/// </summary>
/// <seealso cref="IDataLookupNormalizer" />
public class UpperInvariantLookupNormalizer : IDataLookupNormalizer
{
	#region Private Members
	private readonly ILogger _log;
	#endregion

	#region Constructors		
	/// <summary>
	/// Initializes a new instance of the <see cref="UpperInvariantLookupNormalizer"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	public UpperInvariantLookupNormalizer(ILogger<UpperInvariantLookupNormalizer> logger)
	{
		_log = logger;
	}
	#endregion

	#region IDataAccessLookupNormalizer Members		
	/// <summary>
	/// Normalizes the specified value.
	/// </summary>
	/// <param name="value">The value.</param>
	/// <param name="trim">if set to <c>true</c> the value will be trimmed of leading and trailing whitespace before being normalized.</param>
	/// <returns>
	/// The normalized value.
	/// </returns>
	/// <exception cref="UmbrellaException">There has been a problem normalizing the specified value.</exception>
	public string Normalize(string value, bool trim = true)
	{
		Guard.IsNotNull(value, nameof(value));

		try
		{
			return trim ? value.Normalize().TrimToUpperInvariant() : value.Normalize().ToUpperInvariant();
		}
		catch (Exception exc) when (_log.WriteError(exc, new { value = "Value not logged for security reasons.", trim }, "String normalization to form C failed. The source string has not been logged for security reasons."))
		{
			throw new UmbrellaException("There has been a problem normalizing the specified value.", exc);
		}
	}
	#endregion
}