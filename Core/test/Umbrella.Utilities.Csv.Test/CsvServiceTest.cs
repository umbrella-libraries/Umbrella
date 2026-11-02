using System.Globalization;
using System.Text;
using CsvHelper;
using Microsoft.Extensions.Logging.Abstractions;
using Umbrella.Utilities.Exceptions;

namespace Umbrella.Utilities.Csv.Test;

public sealed class CsvServiceTest
{
	private readonly CsvService _service = new(NullLogger<CsvService>.Instance);

	[Fact]
	public async Task GenerateAsync_Defaults_PreservesEncodingQuotingAndMappedHeaders()
	{
		CsvOrderRow row = new() { CustomerName = " Zoë, \"Q\"\r\n ", TotalAmount = 12.5m };

		byte[] result = await _service.GenerateAsync([row], cancellationToken: TestContext.Current.CancellationToken);

		Assert.Equal("\uFEFFCustomer Name,TotalAmount\r\n\" Zoë, \"\"Q\"\"\r\n \",12.5\r\n", Encoding.UTF8.GetString(result));
	}

	[Fact]
	public async Task GenerateAsync_EmptySequence_WritesHeader()
	{
		byte[] result = await _service.GenerateAsync(Array.Empty<CsvOrderRow>(), cancellationToken: TestContext.Current.CancellationToken);

		Assert.Equal("\uFEFFCustomer Name,TotalAmount\r\n", Encoding.UTF8.GetString(result));
	}

	[Theory]
	[InlineData(" Ada")]
	[InlineData("Ada ")]
	public async Task GenerateAsync_WhitespaceAtFieldEdges_QuotesField(string customerName)
	{
		byte[] result = await _service.GenerateAsync(
			new[] { new CsvOrderRow { CustomerName = customerName, TotalAmount = 12.5m } },
			cancellationToken: TestContext.Current.CancellationToken);

		Assert.Equal($"\uFEFFCustomer Name,TotalAmount\r\n\"{customerName}\",12.5\r\n", Encoding.UTF8.GetString(result));
	}

	[Fact]
	public async Task GenerateAsync_CustomOptions_UsesSeparatorCultureAndNewLineWithoutBom()
	{
		CsvWriterOptions options = new()
		{
			Separator = ';',
			NewLine = "\n",
			FormatProvider = CultureInfo.GetCultureInfo("de-DE")
		};

		byte[] result = await _service.GenerateAsync(
			new[] { new CsvOrderRow { CustomerName = "Ada", TotalAmount = 12.5m } },
			options, TestContext.Current.CancellationToken);

		Assert.Equal("Customer Name;TotalAmount\nAda;12,5\n", Encoding.UTF8.GetString(result));
	}

	[Fact]
	public async Task GenerateAsync_HeaderDisabled_WritesOnlyData()
	{
		byte[] result = await _service.GenerateAsync(
			new[] { new CsvOrderRow { CustomerName = "Ada", TotalAmount = 12.5m } },
			new CsvWriterOptions { HasHeader = false }, TestContext.Current.CancellationToken);

		Assert.Equal("Ada,12.5\r\n", Encoding.UTF8.GetString(result));
	}

	[Fact]
	public async Task GenerateAndParseAsync_GeneratedMapping_RoundTripsAndClosesInput()
	{
		CsvOrderRow row = new() { CustomerName = " Zoë, \"Q\"\r\n ", TotalAmount = 12.5m };
		byte[] csv = await _service.GenerateAsync([row], cancellationToken: TestContext.Current.CancellationToken);
		using MemoryStream stream = new(csv);

		var result = await _service.ParseFromStreamAsync<CsvOrderRow>(stream, cancellationToken: TestContext.Current.CancellationToken);

		Assert.Equal(row, Assert.Single(result));
		Assert.False(stream.CanRead);
	}

	[Fact]
	public async Task ParseFromStreamAsync_CustomOptions_UsesSeparatorCultureAndTrimming()
	{
		using MemoryStream stream = new(Encoding.UTF8.GetBytes("Customer Name;TotalAmount\n Ada ;12,5\n"));
		CsvReaderOptions options = new()
		{
			Separator = ';',
			FormatProvider = CultureInfo.GetCultureInfo("de-DE"),
			TrimWhitespace = true
		};

		var result = await _service.ParseFromStreamAsync<CsvOrderRow>(stream, options, TestContext.Current.CancellationToken);

		Assert.Equal(new CsvOrderRow { CustomerName = "Ada", TotalAmount = 12.5m }, Assert.Single(result));
	}

	[Fact]
	public async Task ParseFromStreamAsync_Utf16WithBom_DecodesInput()
	{
		byte[] csv = [.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes("Customer Name,TotalAmount\r\nZoë,12.5\r\n")];
		using MemoryStream stream = new(csv);

		var result = await _service.ParseFromStreamAsync<CsvOrderRow>(stream, cancellationToken: TestContext.Current.CancellationToken);

		Assert.Equal(new CsvOrderRow { CustomerName = "Zoë", TotalAmount = 12.5m }, Assert.Single(result));
	}

	[Fact]
	public async Task GenerateAndParseAsync_ExternalMap_UsesColumnNamesAndFormats()
	{
		ExternalOrder row = new() { CustomerName = "Ada", OrderedOn = new DateTime(2026, 11, 2) };
		byte[] csv = await _service.GenerateAsync([row], cancellationToken: TestContext.Current.CancellationToken);
		Assert.Equal("\uFEFFCustomer Name,OrderedOn\r\nAda,20261102\r\n", Encoding.UTF8.GetString(csv));
		using MemoryStream stream = new(csv);

		var result = await _service.ParseFromStreamAsync<ExternalOrder>(stream, cancellationToken: TestContext.Current.CancellationToken);

		Assert.Equal(row, Assert.Single(result));
	}

	[Theory]
	[InlineData("Customer Name,TotalAmount\r\n\"Ada,12.5\r\n")]
	[InlineData("Customer Name,TotalAmount\r\nAda\r\n")]
	[InlineData("Customer Name,TotalAmount\r\nAda,invalid\r\n")]
	[InlineData("Customer Name\r\nAda\r\n")]
	public async Task ParseFromStreamAsync_InvalidInput_WrapsFailureAndClosesInput(string csv)
	{
		using MemoryStream stream = new(Encoding.UTF8.GetBytes(csv));

		var exception = await Assert.ThrowsAnyAsync<UmbrellaException>(() =>
			_service.ParseFromStreamAsync<CsvOrderRow>(stream, cancellationToken: TestContext.Current.CancellationToken));

		Assert.Equal("There has been a problem parsing the CSV.", exception.Message);
		Assert.NotNull(exception.InnerException);
		Assert.False(stream.CanRead);
	}

	[Fact]
	public async Task ParseFromStreamAsync_InvalidOptions_WrapsFailureAndClosesInput()
	{
		using MemoryStream stream = new();

		var exception = await Assert.ThrowsAnyAsync<UmbrellaException>(() =>
			_service.ParseFromStreamAsync<CsvOrderRow>(stream, new CsvReaderOptions { BufferSize = 1 }, TestContext.Current.CancellationToken));

		_ = Assert.IsType<ArgumentOutOfRangeException>(exception.InnerException);
		Assert.False(stream.CanRead);
	}

	[Fact]
	public async Task GenerateAsync_UnmappedType_WrapsMissingWriterFailure()
	{
		var exception = await Assert.ThrowsAnyAsync<UmbrellaException>(async () =>
			await _service.GenerateAsync(new[] { new UnmappedRow("Ada") }, cancellationToken: TestContext.Current.CancellationToken));

		Assert.Equal("There has been a problem generating the CSV.", exception.Message);
		_ = Assert.IsType<InvalidOperationException>(exception.InnerException);
	}

	[Fact]
	public async Task ParseFromStreamAsync_UnmappedType_WrapsMissingMapperFailure()
	{
		using MemoryStream stream = new(Encoding.UTF8.GetBytes("Name\r\nAda\r\n"));

		var exception = await Assert.ThrowsAnyAsync<UmbrellaException>(() =>
			_service.ParseFromStreamAsync<UnmappedRow>(stream, cancellationToken: TestContext.Current.CancellationToken));

		_ = Assert.IsType<InvalidOperationException>(exception.InnerException);
	}

	[Fact]
	public async Task GenerateAsync_CancelledBeforeCall_PropagatesCancellation()
	{
		using CancellationTokenSource cts = new();
		await cts.CancelAsync();

		_ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
			await _service.GenerateAsync(Array.Empty<CsvOrderRow>(), cancellationToken: cts.Token));
	}

	[Fact]
	public async Task GenerateAsync_CancelledDuringEnumeration_PropagatesCancellation()
	{
		using CancellationTokenSource cts = new();

		_ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
			await _service.GenerateAsync(EnumerateRows(), cancellationToken: cts.Token));

		IEnumerable<CsvOrderRow> EnumerateRows()
		{
			yield return new() { CustomerName = "Ada", TotalAmount = 12.5m };
			cts.Cancel();
			yield return new() { CustomerName = "Grace", TotalAmount = 20m };
		}
	}

	[Fact]
	public async Task ParseFromStreamAsync_CancelledBeforeCall_PropagatesCancellationAndLeavesInputOpen()
	{
		using CancellationTokenSource cts = new();
		await cts.CancelAsync();
		using MemoryStream stream = new();

		_ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
			_service.ParseFromStreamAsync<CsvOrderRow>(stream, cancellationToken: cts.Token));

		Assert.True(stream.CanRead);
	}

	[Fact]
	public async Task ParseFromStreamAsync_CancelledDuringParsing_PropagatesCancellationAndClosesInput()
	{
		using CancellationTokenSource cts = new();
		using MemoryStream stream = new(Encoding.UTF8.GetBytes("Customer Name,TotalAmount\r\nAda,invalid\r\nGrace,20\r\n"));
		CsvReaderOptions options = new()
		{
			OnRecordError = _ =>
			{
				cts.Cancel();
				return true;
			}
		};

		_ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
			_service.ParseFromStreamAsync<CsvOrderRow>(stream, options, cts.Token));

		Assert.False(stream.CanRead);
	}
}

[CsvRecord]
internal sealed partial record CsvOrderRow
{
	[CsvColumn("Customer Name")]
	public required string CustomerName { get; init; }

	public required decimal TotalAmount { get; init; }
}

internal sealed record ExternalOrder
{
	public required string CustomerName { get; init; }

	public required DateTime OrderedOn { get; init; }
}

[CsvRecordMap(typeof(ExternalOrder))]
internal sealed partial class ExternalOrderMap
{
	[CsvColumn("Customer Name")]
	public string CustomerName { get; set; } = "";

	[CsvFormat("yyyyMMdd")]
	public DateTime OrderedOn { get; set; }
}

internal sealed record UnmappedRow(string Name);
