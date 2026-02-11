using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;
using Umbrella.Utilities.Csv.Abstractions;
using Umbrella.Utilities.Csv.Exceptions;
using Umbrella.Utilities.Csv.Extensions;

namespace Umbrella.Utilities.Csv;

/// <summary>
/// A service for generating and parsing CSV data.
/// </summary>
public class CsvService : ICsvService
{
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
	public async ValueTask<byte[]> GenerateAsync<TModel>(IEnumerable<TModel> items, ClassMap<TModel>? map = null, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			using MemoryStream ms = new();
			using StreamWriter sw = new(ms, new UTF8Encoding(true));

			using CsvWriter csvWriter = new(sw, CultureInfo.InvariantCulture);

			if (map is not null)
				csvWriter.Context.RegisterClassMap(map);
			else
				csvWriter.Context.RegisterClassMap(FriendlyHeaderNameClassMap<TModel>.Instance);

			csvWriter.WriteRecords(items);

			await sw.FlushAsync(cancellationToken);

			return ms.ToArray();
		}
		catch (Exception exc) when (_logger.WriteError(exc))
		{
			throw new UmbrellaCsvException("There has been a problem generating the CSV.", exc);
		}
	}

	/// <inheritdoc />
	public async Task<IReadOnlyCollection<T>> ParseFromStreamAsync<T>(Stream stream, CsvConfiguration? config = default, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			using var reader = new StreamReader(stream);

			config ??= new CsvConfiguration(CultureInfo.InvariantCulture);

			using var csv = new CsvReader(reader, config);

			IAsyncEnumerable<T> sequence = csv.GetRecordsAsync<T>(cancellationToken);

			List<T> result = [];

			await foreach (var item in sequence)
			{
				result.Add(item);
			}

			return result;
		}
		catch (Exception exc) when (_logger.WriteError(exc))
		{
			throw new UmbrellaCsvException("There has been a problem parsing the CSV.", exc);
		}
	}
}

file sealed class FriendlyHeaderNameClassMap<T> : ClassMap<T>
{
	public static FriendlyHeaderNameClassMap<T> Instance { get; } = new();

	public FriendlyHeaderNameClassMap()
	{
		AutoMap(CultureInfo.InvariantCulture);
		MemberMaps.EnsureFriendlyHeaderNames();
	}
}