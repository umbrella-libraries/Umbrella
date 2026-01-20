#if NET8_0_OR_GREATER
using System.Text;
#endif

namespace Umbrella.DataAccess.Abstractions;

/// <summary>
/// Common error messages used by EF repositories.
/// </summary>
public static class ErrorMessages
{
	private const string InvalidPropertyStringLengthErrorMessageFormatInternal = "The {0} value must be between {1} and {2} characters in length.";
	private const string InvalidPropertyNumberRangeErrorMessageFormatInternal = "The {0} value must be between {1} and {2}.";
	private const string ConcurrencyExceptionErrorMessageFormatInternal = "A concurrency error has occurred whilst trying to save the item with id {0} or one of its dependants.";

	/// <summary>
	/// The bulk action concurrency exception error message
	/// </summary>
	public const string BulkActionConcurrencyExceptionErrorMessage = "A concurrency error has occurred whilst trying to update the items.";

#if NET8_0_OR_GREATER
	/// <summary>
	/// The invalid property string length error message format
	/// </summary>
	public static readonly CompositeFormat InvalidPropertyStringLengthErrorMessageFormat = CompositeFormat.Parse(InvalidPropertyStringLengthErrorMessageFormatInternal);

	/// <summary>
	/// The invalid property number range error message format
	/// </summary>
	public static readonly CompositeFormat InvalidPropertyNumberRangeErrorMessageFormat = CompositeFormat.Parse(InvalidPropertyNumberRangeErrorMessageFormatInternal);

	/// <summary>
	/// The concurrency exception error message format
	/// </summary>
	public static readonly CompositeFormat ConcurrencyExceptionErrorMessageFormat = CompositeFormat.Parse(ConcurrencyExceptionErrorMessageFormatInternal);
#else
	/// <summary>
	/// The invalid property string length error message format
	/// </summary>
	public const string InvalidPropertyStringLengthErrorMessageFormat = InvalidPropertyStringLengthErrorMessageFormatInternal;

	/// <summary>
	/// The invalid property number range error message format
	/// </summary>
	public const string InvalidPropertyNumberRangeErrorMessageFormat = InvalidPropertyNumberRangeErrorMessageFormatInternal;

	/// <summary>
	/// The concurrency exception error message format
	/// </summary>
	public const string ConcurrencyExceptionErrorMessageFormat = ConcurrencyExceptionErrorMessageFormatInternal;
#endif
}