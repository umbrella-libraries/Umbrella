namespace Umbrella.Analyzers.Test;

public class UA014_CollectionsMustBeReadOnlyTests : AnalyzerTestBase<UmbrellaModelStandardsAnalyzer>
{
	[Theory]
	[InlineData("List<string>")]
	[InlineData("Dictionary<int, string>")]
	[InlineData("ICollection<string>")]
	[InlineData("IList<string>")]
	[InlineData("ISet<string>")]
	[InlineData("IDictionary<int, string>")]
	[InlineData("System.Collections.IList")]
	[InlineData("string[]")]
	public async Task MutableCollectionProperty_ShouldTriggerDiagnostic(string propertyType)
	{
		ArgumentNullException.ThrowIfNull(propertyType);

		string source = $$"""
			using System.Collections.Generic;

			public record UserModel
			{
				public required {{propertyType}} Items { get; init; }
			}
			""";

		await VerifyAnalyzerAsync(
			source,
			Diagnostic(
				UmbrellaModelStandardsAnalyzer.CollectionsMustBeReadOnlyRule,
				5,
				19 + propertyType.Length,
				"Items",
				"UserModel"));
	}

	[Theory]
	[InlineData("IEnumerable<string>")]
	[InlineData("IReadOnlyCollection<string>")]
	[InlineData("IReadOnlyList<string>")]
	[InlineData("IReadOnlySet<string>")]
	[InlineData("IReadOnlyDictionary<int, string>")]
	[InlineData("System.Collections.IEnumerable")]
	public async Task ReadOnlyCollectionInterfaceProperty_ShouldNotTriggerDiagnostic(string propertyType)
	{
		string source = $$"""
			using System.Collections.Generic;

			public record UserModel
			{
				public required {{propertyType}} Items { get; init; }
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task CustomReadOnlyInterfaceProperty_ShouldNotTriggerDiagnostic()
	{
		const string source = """
			using System.Collections.Generic;

			public interface IReadOnlyItems<T> : IEnumerable<T>
			{
			}

			public record UserModel
			{
				public required IReadOnlyItems<string> Items { get; init; }
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task KnownReadOnlyConcreteCollectionProperties_ShouldNotTriggerDiagnostic()
	{
		const string source = """
			using System.Collections.Generic;
			using System.Collections.ObjectModel;

			namespace System.Collections.Immutable
			{
				public class ImmutableList<T> : IEnumerable<T>
				{
				}
			}

			namespace System.Collections.Frozen
			{
				public class FrozenDictionary<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
				{
				}
			}

			public record UserModel
			{
				public required ReadOnlyCollection<int> ReadOnlyItems { get; init; }
				public required System.Collections.Immutable.ImmutableList<int> ImmutableItems { get; init; }
				public required System.Collections.Frozen.FrozenDictionary<int, string> FrozenItems { get; init; }
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task CustomConcreteCollectionWithReadOnlyName_ShouldTriggerDiagnostic()
	{
		const string source = """
			using System.Collections.Generic;

			public class ReadOnlyItems<T> : IEnumerable<T>
			{
			}

			public record UserModel
			{
				public required ReadOnlyItems<string> Items { get; init; }
			}
			""";

		await VerifyAnalyzerAsync(
			source,
			Diagnostic(UmbrellaModelStandardsAnalyzer.CollectionsMustBeReadOnlyRule, 9, 40, "Items", "UserModel"));
	}

	[Fact]
	public async Task StringProperties_ShouldNotBeTreatedAsCollections()
	{
		const string source = """
			public record UserModel
			{
				public required string Name { get; init; }
				public required string? OptionalName { get; init; }
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task MutableCollectionProperty_WithOptOutAttribute_ShouldNotTriggerDiagnostic()
	{
		const string source = """
			using System;
			using System.Collections.Generic;

			public record UserModel
			{
				[Umbrella.Analyzers.UmbrellaAllowMutableProperty("Collection is mutable UI form state.")]
				public required List<string> Items { get; init; }
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}
}
