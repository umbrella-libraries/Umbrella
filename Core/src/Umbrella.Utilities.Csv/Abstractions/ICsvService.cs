using CsvHelper;

namespace Umbrella.Utilities.Csv.Abstractions;

/// <summary>
/// A service for generating and parsing CSV data.
/// </summary>
public interface ICsvService
{
	/// <summary>
	/// Generates a CSV file from the specified items.
	/// </summary>
	/// <remarks>
	/// The model must have a CsvHelper 34 generated or registered writer. Use <c>[CsvRecord]</c> on a partial type,
	/// or an external <c>[CsvRecordMap(typeof(TModel))]</c> partial map. Column names default to member names;
	/// use <c>[CsvColumn("Friendly Name")]</c> to specify friendly headers.
	/// </remarks>
	/// <typeparam name="TModel">The type of the model to generate the CSV from.</typeparam>
	/// <param name="items">The items to generate the CSV from.</param>
	/// <param name="options">The writer options. If <see langword="null"/>, uses invariant, comma-separated CSV with a UTF-8 BOM,
	/// CRLF line endings, and quoting for fields that need it or have leading or trailing whitespace.
	/// Explicit options are used as supplied.</param>
	/// <param name="cancellationToken">The cancellation token to observe while generating the CSV.</param>
	/// <returns>The generated CSV file as a byte array.</returns>
	ValueTask<byte[]> GenerateAsync<TModel>(IEnumerable<TModel> items, CsvWriterOptions? options = null, CancellationToken cancellationToken = default);

	/// <summary>
	/// Parses a CSV file from the specified stream into a collection of items.
	/// </summary>
	/// <remarks>
	/// The model must have a CsvHelper 34 generated or registered mapper, as with <c>[CsvRecord]</c> or
	/// <c>[CsvRecordMap(typeof(T))]</c>. The input stream is disposed when parsing completes or fails.
	/// </remarks>
	/// <typeparam name="T">The type of the items to parse from the CSV.</typeparam>
	/// <param name="stream">The stream containing the CSV data.</param>
	/// <param name="options">The reader options. If <see langword="null"/>, uses invariant, comma-separated CSV and throws
	/// for malformed data and missing fields. Explicit options are used as supplied.</param>
	/// <param name="cancellationToken">The cancellation token to observe while parsing the CSV.</param>
	/// <returns>The parsed items as a read-only collection.</returns>
	Task<IReadOnlyCollection<T>> ParseFromStreamAsync<T>(Stream stream, CsvReaderOptions? options = null, CancellationToken cancellationToken = default);
}
