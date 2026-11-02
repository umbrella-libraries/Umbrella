using CommunityToolkit.Diagnostics;
using CsvHelper;
using Microsoft.Extensions.Logging;
using Umbrella.Utilities.Csv.Abstractions;
using Umbrella.Utilities.Csv.Exceptions;

namespace Umbrella.Utilities.Csv;

/// <summary>
/// A service for generating and parsing CSV data.
/// </summary>
public class CsvService : ICsvService
{
	private static readonly CsvWriterOptions _defaultWriterOptions = new()
	{
		WriteByteOrderMark = true,
		Quoting = CsvQuoting.WhenNeededOrWhitespace
	};

	private static readonly CsvReaderOptions _defaultReaderOptions = new()
	{
		Separator = ',',
		MalformedData = CsvMalformedDataHandling.Throw,
		MissingField = CsvMissingFieldHandling.Throw
	};

	private readonly ILogger _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="CsvService"/> class.
	/// </summary>
	/// <param name="logger">The logger to use for logging errors.</param>
	public CsvService(ILogger<CsvService> logger)
	{
		_logger = logger;
	}

	/// <inheritdoc />
	public async ValueTask<byte[]> GenerateAsync<TModel>(IEnumerable<TModel> items, CsvWriterOptions? options = null, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(items);

		try
		{
			using MemoryStream ms = new();
			await using var csvWriter = CsvWriter.Create(ms, options ?? _defaultWriterOptions, leaveOpen: true);

			await csvWriter.WriteRecordsAsync(items, cancellationToken);
			cancellationToken.ThrowIfCancellationRequested();
			await csvWriter.FlushAsync(cancellationToken);

			return ms.ToArray();
		}
		catch (Exception exc) when (exc is not OperationCanceledException && _logger.WriteError(exc, new { options }))
		{
			throw new UmbrellaCsvException("There has been a problem generating the CSV.", exc);
		}
	}

	/// <inheritdoc />
	public async Task<IReadOnlyCollection<T>> ParseFromStreamAsync<T>(Stream stream, CsvReaderOptions? options = null, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(stream);

		try
		{
			await using var inputStream = stream;
			await using var csv = CsvReader.Create(inputStream, options ?? _defaultReaderOptions, leaveOpen: true);

			IAsyncEnumerable<T> sequence = csv.GetRecordsAsync<T>(cancellationToken);

			List<T> result = [];

			await foreach (var item in sequence)
			{
				cancellationToken.ThrowIfCancellationRequested();
				result.Add(item);
			}

			cancellationToken.ThrowIfCancellationRequested();

			return result;
		}
		catch (Exception exc) when (exc is not OperationCanceledException && _logger.WriteError(exc, new { options }))
		{
			throw new UmbrellaCsvException("There has been a problem parsing the CSV.", exc);
		}
	}
}
