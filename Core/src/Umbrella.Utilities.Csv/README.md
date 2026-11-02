# Umbrella.Utilities.Csv

`ICsvService` generates and parses CSV using CsvHelper 34. Register it with
`services.AddUmbrellaCsv()`.

## Migrating from CsvHelper 33

- Replace `ClassMap<T>` and `RegisterClassMap` with `[CsvRecord]` on a partial
  model, or `[CsvRecordMap(typeof(T))]` on a separate partial map for a type you
  cannot change. The generator registers the mapping automatically.
- `GenerateAsync` now accepts `CsvWriterOptions` instead of a class map.
- `ParseFromStreamAsync` now accepts `CsvReaderOptions` instead of
  `CsvConfiguration`. Rename named `config:` arguments to `options:`.
- Remove `using CsvHelper.Configuration;` and use `using CsvHelper;`.
- The runtime `EnsureFriendlyHeaderNames` extension has been removed. Headers
  now use the generated mapping's names. Add `[CsvColumn("Friendly Name")]`
  to retain an existing humanized or custom header.
- Models without generated or registered mappings now fail at runtime. Ensure
  the project declaring the model or map has a direct `CsvHelper` package
  reference so it runs the source generator included in that package.

```csharp
using CsvHelper;

[CsvRecord]
public sealed partial record Order
{
	[CsvColumn("Customer Name")]
	public required string CustomerName { get; init; }

	[CsvColumn("Total Amount")]
	public required decimal TotalAmount { get; init; }
}
```

```csharp
byte[] csv = await csvService.GenerateAsync(
	new[] { new Order { CustomerName = "Ada", TotalAmount = 12.5m } },
	cancellationToken: cancellationToken);

using var stream = new MemoryStream(csv);
IReadOnlyCollection<Order> orders = await csvService.ParseFromStreamAsync<Order>(
	stream, cancellationToken: cancellationToken);
```

The default output remains invariant CSV with a comma separator, UTF-8 BOM,
CRLF line endings, and quoting of fields with leading or trailing whitespace.
Default parsing uses a comma separator and rejects malformed data and missing
fields. Parsing disposes the input stream, including on failure. Cancellation
propagates without being wrapped; other operational failures are logged and
wrapped in an Umbrella exception.

Explicit options are used as supplied, including CsvHelper's own defaults for
properties you omit. For example, a new `CsvWriterOptions` omits the BOM, and a
new `CsvReaderOptions` detects the separator and recovers malformed quoting.
Set strictness explicitly if you need it with custom options:

```csharp
var writerOptions = new CsvWriterOptions
{
	Separator = ';',
	WriteByteOrderMark = true,
	Quoting = CsvQuoting.WhenNeededOrWhitespace
};

var readerOptions = new CsvReaderOptions
{
	Separator = ';',
	MalformedData = CsvMalformedDataHandling.Throw,
	MissingField = CsvMissingFieldHandling.Throw
};
```

CsvHelper 34 also changes header matching, enum and boolean parsing, UTC date
handling, and which members are mapped. See the
[CsvHelper migration guide](https://joshclose.github.io/CsvHelper/migration/v34.0.0.html)
when migrating consumer models.
