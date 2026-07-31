using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using Umbrella.DynamicImage.Abstractions;

namespace Umbrella.Generators.DynamicImage.Test;

public class DynamicImageRazorSourceGeneratorTests
{
	[Fact]
	public void RazorComponentsFromNamedCatalogsEmitNamedAndAggregateCatalogs()
	{
		AdditionalText[] files =
		[
			new TestAdditionalText("C:/app/Server/_Imports.razor", "@using Umbrella.AspNetCore.Blazor.Components.DynamicImage"),
			new TestAdditionalText("C:/app/Server/Test.razor", """
<UmbrellaDynamicImage Url="/images/server.jpg"
                      WidthRequest="321"
                      HeightRequest="123"
                      MaxPixelDensity="1" />
"""),
			new TestAdditionalText("C:/app/Client/_Imports.razor", "@using Umbrella.AspNetCore.Blazor.Components.DynamicImage"),
			new TestAdditionalText("C:/app/Client/Test.razor", """
<UmbrellaDynamicImage Url="/images/client.jpg"
                      WidthRequest="400"
                      HeightRequest="200"
                      MaxPixelDensity="1"
                      ResizeMode="DynamicResizeMode.CropFocalPoint"
                      ImageFormat="DynamicImageFormat.WebP" />
""")
		];
		var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			["C:/app/Server/_Imports.razor"] = "Server",
			["C:/app/Server/Test.razor"] = "Server",
			["C:/app/Client/_Imports.razor"] = "Client",
			["C:/app/Client/Test.razor"] = "Client"
		};

		Assembly assembly = GenerateAssembly(files, metadata, out ImmutableArray<Diagnostic> diagnostics);

		Assert.Empty(diagnostics);
		Assert.Equal(
			[new DynamicImageVariant(321, 123, DynamicResizeMode.Crop, DynamicImageFormat.Jpeg)],
			GetVariants(assembly, "ServerDynamicImageVariantCatalog"));
		Assert.Equal(
			[new DynamicImageVariant(400, 200, DynamicResizeMode.CropFocalPoint, DynamicImageFormat.WebP)],
			GetVariants(assembly, "ClientDynamicImageVariantCatalog"));
		Assert.Equal(
			[
				new DynamicImageVariant(321, 123, DynamicResizeMode.Crop, DynamicImageFormat.Jpeg),
				new DynamicImageVariant(400, 200, DynamicResizeMode.CropFocalPoint, DynamicImageFormat.WebP)
			],
			GetVariants(assembly, "DynamicImageVariantCatalog"));
	}

	[Fact]
	public void RazorComponentWithExpressionSkipsEntireUsage()
	{
		AdditionalText[] files =
		[
			new TestAdditionalText("C:/app/_Imports.razor", "@using Umbrella.AspNetCore.Blazor.Components.DynamicImage"),
			new TestAdditionalText("C:/app/Test.razor", """
<UmbrellaDynamicImage WidthRequest="@width"
                      HeightRequest="123"
                      MaxPixelDensity="1" />
""")
		];

		Assembly assembly = GenerateAssembly(files, CreateCatalogMetadata(files, "Server"), out ImmutableArray<Diagnostic> diagnostics);

		Assert.Empty(diagnostics);
		Assert.Empty(GetVariants(assembly, "ServerDynamicImageVariantCatalog"));
		Assert.Empty(GetVariants(assembly, "DynamicImageVariantCatalog"));
	}

	[Fact]
	public void RazorComponentWithDynamicEnumBindingSkipsEntireUsageInsteadOfUsingDefault()
	{
		AdditionalText[] files =
		[
			new TestAdditionalText("C:/app/_Imports.razor", "@using Umbrella.AspNetCore.Blazor.Components.DynamicImage"),
			new TestAdditionalText("C:/app/Test.razor", """
<UmbrellaDynamicImage WidthRequest="200"
                      HeightRequest="100"
                      MaxPixelDensity="1"
                      ResizeMode="@Model.ResizeMode" />
""")
		];

		Assembly assembly = GenerateAssembly(files, CreateCatalogMetadata(files, "Server"), out ImmutableArray<Diagnostic> diagnostics);

		Assert.Empty(diagnostics);
		Assert.Empty(GetVariants(assembly, "ServerDynamicImageVariantCatalog"));
		Assert.Empty(GetVariants(assembly, "DynamicImageVariantCatalog"));
	}

	[Fact]
	public void RazorTagHelperWithMixedStringExpressionSkipsEntireUsage()
	{
		AdditionalText[] files =
		[
			new TestAdditionalText("C:/app/Views/_ViewImports.cshtml", "@addTagHelper *, Umbrella.AspNetCore.WebUtilities.DynamicImage"),
			new TestAdditionalText("C:/app/Views/Test.cshtml", """
<dynamic-image src="/images/test.jpg"
               width-request="200"
               height-request="100"
               image-density="1"
               size-widths="100,@Model.Width" />
""")
		];

		Assembly assembly = GenerateAssembly(files, CreateCatalogMetadata(files, "Server"), out ImmutableArray<Diagnostic> diagnostics);

		Assert.Empty(diagnostics);
		Assert.Empty(GetVariants(assembly, "ServerDynamicImageVariantCatalog"));
		Assert.Empty(GetVariants(assembly, "DynamicImageVariantCatalog"));
	}

	[Fact]
	public void RazorSizeExpansionIsDeduplicatedAndExternalUrlsAreIgnored()
	{
		AdditionalText[] files =
		[
			new TestAdditionalText("C:/app/_Imports.razor", "@using Umbrella.AspNetCore.Blazor.Components.DynamicImage"),
			new TestAdditionalText("C:/app/Test.razor", """
<UmbrellaDynamicImage Url="/images/local.jpg"
                      WidthRequest="400"
                      HeightRequest="200"
                      MaxPixelDensity="2"
                      SizeWidths="100, 200" />
<UmbrellaDynamicImage Url="https://cdn.example.com/external.jpg"
                      WidthRequest="999"
                      HeightRequest="999" />
""")
		];

		Assembly assembly = GenerateAssembly(files, CreateCatalogMetadata(files, "Server"), out ImmutableArray<Diagnostic> diagnostics);

		Assert.Empty(diagnostics);
		Assert.Equal(
			[
				new DynamicImageVariant(100, 50, DynamicResizeMode.Crop, DynamicImageFormat.Jpeg),
				new DynamicImageVariant(200, 100, DynamicResizeMode.Crop, DynamicImageFormat.Jpeg),
				new DynamicImageVariant(400, 200, DynamicResizeMode.Crop, DynamicImageFormat.Jpeg)
			],
			GetVariants(assembly, "ServerDynamicImageVariantCatalog"));
	}

	[Fact]
	public void CommentsCodeBlocksAndUnimportedComponentsAreIgnored()
	{
		AdditionalText[] files =
		[
			new TestAdditionalText("C:/app/Test.razor", """
@* <UmbrellaDynamicImage WidthRequest="100" HeightRequest="50" /> *@
<!-- <UmbrellaDynamicImage WidthRequest="200" HeightRequest="50" /> -->
@code {
    private const string Markup = "<UmbrellaDynamicImage WidthRequest=\"300\" />";
}
<UmbrellaDynamicImage WidthRequest="400" HeightRequest="50" />
""")
		];

		Assembly assembly = GenerateAssembly(files, CreateCatalogMetadata(files, "Server"), out ImmutableArray<Diagnostic> diagnostics);

		Assert.Empty(diagnostics);
		Assert.Empty(GetVariants(assembly, "ServerDynamicImageVariantCatalog"));
	}

	[Fact]
	public void ActiveMvcTagHelpersEmitVariantsAndRemovedTagHelpersDoNot()
	{
		AdditionalText[] files =
		[
			new TestAdditionalText("C:/app/Views/_ViewImports.cshtml", "@addTagHelper *, Umbrella.AspNetCore.WebUtilities.DynamicImage"),
			new TestAdditionalText("C:/app/Views/Active.cshtml", """
<dynamic-image src="/images/test.jpg"
               width-request="200"
               height-request="100"
               image-density="1"
               image-format="DynamicImageFormat.Png"
               size-widths="100,200" />
"""),
			new TestAdditionalText("C:/app/Views/Removed/_ViewImports.cshtml", "@removeTagHelper *, Umbrella.AspNetCore.WebUtilities.DynamicImage"),
			new TestAdditionalText("C:/app/Views/Removed/Inactive.cshtml", """
<dynamic-image width-request="999" height-request="999" />
""")
		];

		Assembly assembly = GenerateAssembly(files, CreateCatalogMetadata(files, "Server"), out ImmutableArray<Diagnostic> diagnostics);

		Assert.Empty(diagnostics);
		Assert.Equal(
			[
				new DynamicImageVariant(100, 50, DynamicResizeMode.Crop, DynamicImageFormat.Png),
				new DynamicImageVariant(200, 100, DynamicResizeMode.Crop, DynamicImageFormat.Png)
			],
			GetVariants(assembly, "ServerDynamicImageVariantCatalog"));
	}

	[Fact]
	public void MvcTagHelperDirectivesApplyOnlyToMatchingTypes()
	{
		AdditionalText[] files =
		[
			new TestAdditionalText("C:/app/Views/_ViewImports.cshtml", "@addTagHelper *, Umbrella.AspNetCore.WebUtilities.DynamicImage"),
			new TestAdditionalText(
				"C:/app/Views/Nested/_ViewImports.cshtml",
				"@removeTagHelper Umbrella.AspNetCore.WebUtilities.DynamicImage.Mvc.TagHelpers.DynamicImagePictureSourceTagHelper, Umbrella.AspNetCore.WebUtilities.DynamicImage"),
			new TestAdditionalText("C:/app/Views/Nested/Test.cshtml", """
<dynamic-image src="/images/test.jpg"
               width-request="210"
               height-request="110"
               image-density="1" />
<dynamic-source src="/images/ignored.jpg"
                width-request="998"
                height-request="998"
                image-density="1" />
"""),
			new TestAdditionalText(
				"C:/isolated/Views/_ViewImports.cshtml",
				"@addTagHelper Umbrella.AspNetCore.WebUtilities.DynamicImage.Mvc.TagHelpers.UnrelatedTagHelper, Umbrella.AspNetCore.WebUtilities.DynamicImage"),
			new TestAdditionalText("C:/isolated/Views/Test.cshtml", """
<dynamic-image src="/images/ignored.jpg"
               width-request="999"
               height-request="999"
               image-density="1" />
""")
		];

		Assembly assembly = GenerateAssembly(files, CreateCatalogMetadata(files, "Server"), out ImmutableArray<Diagnostic> diagnostics);

		Assert.Empty(diagnostics);
		Assert.Equal(
			[new DynamicImageVariant(210, 110, DynamicResizeMode.Crop, DynamicImageFormat.Jpeg)],
			GetVariants(assembly, "ServerDynamicImageVariantCatalog"));
	}

	[Fact]
	public void SameRazorFileOwnedByTwoCatalogsReportsUwdi005()
	{
		AdditionalText[] files =
		[
			new TestAdditionalText("C:/app/Test.razor", "<p>Test</p>"),
			new TestAdditionalText("C:/app/Test.razor", "<p>Test</p>")
		];
		var optionsProvider = new DuplicatePathOptionsProvider(files[0], "Client", files[1], "Server");
		CSharpCompilation compilation = CreateCompilation();
		var generator = new DynamicImageComponentVariantSourceGenerator();
		GeneratorDriver driver = CSharpGeneratorDriver.Create(
			[generator.AsSourceGenerator()],
			files,
			optionsProvider: optionsProvider);

		driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);
		ImmutableArray<Diagnostic> diagnostics = driver.GetRunResult().Diagnostics;

		Diagnostic diagnostic = Assert.Single(diagnostics);
		Assert.Equal("UWDI005", diagnostic.Id);
	}

	[Fact]
	public void EmptyCatalogNameReportsUwdi005()
	{
		AdditionalText[] files = [new TestAdditionalText("C:/app/Test.razor", "<p>Test</p>")];
		CSharpCompilation compilation = CreateCompilation();
		var generator = new DynamicImageComponentVariantSourceGenerator();
		GeneratorDriver driver = CSharpGeneratorDriver.Create(
			[generator.AsSourceGenerator()],
			files,
			optionsProvider: new TestOptionsProvider(new Dictionary<string, string>
			{
				[files[0].Path] = string.Empty
			}, "Server", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { files[0].Path }));

		driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);

		Diagnostic diagnostic = Assert.Single(driver.GetRunResult().Diagnostics);
		Assert.Equal("UWDI005", diagnostic.Id);
	}

	[Fact]
	public void CatalogNamesDifferingOnlyByCaseReportUwdi005()
	{
		AdditionalText[] files =
		[
			new TestAdditionalText("C:/app/Client/One.razor", "<p>One</p>"),
			new TestAdditionalText("C:/app/client/Two.razor", "<p>Two</p>")
		];
		var metadata = new Dictionary<string, string>
		{
			[files[0].Path] = "Client",
			[files[1].Path] = "client"
		};
		CSharpCompilation compilation = CreateCompilation();
		var generator = new DynamicImageComponentVariantSourceGenerator();
		GeneratorDriver driver = CSharpGeneratorDriver.Create(
			[generator.AsSourceGenerator()],
			files,
			optionsProvider: new TestOptionsProvider(metadata, "Server"));

		driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);

		Diagnostic diagnostic = Assert.Single(driver.GetRunResult().Diagnostics);
		Assert.Equal("UWDI005", diagnostic.Id);
	}

	private static Dictionary<string, string> CreateCatalogMetadata(IEnumerable<AdditionalText> files, string catalogName)
		=> files.ToDictionary(x => x.Path, _ => catalogName, StringComparer.OrdinalIgnoreCase);

	private static Assembly GenerateAssembly(
		AdditionalText[] files,
		IReadOnlyDictionary<string, string> metadata,
		out ImmutableArray<Diagnostic> diagnostics)
	{
		CSharpCompilation compilation = CreateCompilation();
		var generator = new DynamicImageComponentVariantSourceGenerator();
		var optionsProvider = new TestOptionsProvider(metadata, "Server");
		GeneratorDriver driver = CSharpGeneratorDriver.Create(
			[generator.AsSourceGenerator()],
			files,
			optionsProvider: optionsProvider);

		driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation outputCompilation, out diagnostics);
		diagnostics = [.. diagnostics, .. driver.GetRunResult().Diagnostics];

		using var stream = new MemoryStream();
		EmitResult emitResult = outputCompilation.Emit(stream);

		if (!emitResult.Success)
			throw new Xunit.Sdk.XunitException(string.Join(Environment.NewLine, emitResult.Diagnostics));

		return Assembly.Load(stream.ToArray());
	}

	private static DynamicImageVariant[] GetVariants(Assembly assembly, string typeName)
	{
		Type type = assembly.GetType($"Umbrella.Generated.DynamicImage.{typeName}", throwOnError: true)!;
		return ((IEnumerable<DynamicImageVariant>)type.GetField("All", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!)
			.OrderBy(x => x.Width)
			.ThenBy(x => x.Height)
			.ThenBy(x => (int)x.ResizeMode)
			.ThenBy(x => (int)x.Format)
			.ToArray();
	}

	private static CSharpCompilation CreateCompilation()
	{
		const string source = """
namespace Umbrella.AspNetCore.Blazor.Components.DynamicImage
{
    public class UmbrellaDynamicImage { }
}

namespace Umbrella.AspNetCore.WebUtilities.DynamicImage.Mvc.TagHelpers
{
    public class DynamicImageTagHelper { }
    public class DynamicImagePictureSourceTagHelper { }
}
""";
		SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(SourceText.From(source, Encoding.UTF8));
		string[] referencePaths =
		[
			.. AppDomain.CurrentDomain.GetAssemblies()
				.Where(x => !x.IsDynamic && !string.IsNullOrWhiteSpace(x.Location))
				.Select(x => x.Location),
			.. (((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))?.Split(Path.PathSeparator) ?? []),
			typeof(DynamicImageVariant).Assembly.Location
		];
		MetadataReference[] references = [.. referencePaths
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.Select(x => MetadataReference.CreateFromFile(x))];

		return CSharpCompilation.Create(
			"RazorGeneratorConsumer",
			[syntaxTree],
			references,
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
	}

	private sealed class TestAdditionalText : AdditionalText
	{
		private readonly SourceText _text;
		public override string Path { get; }

		public TestAdditionalText(string path, string text)
		{
			Path = path;
			_text = SourceText.From(text, Encoding.UTF8);
		}

		public override SourceText GetText(CancellationToken cancellationToken = default) => _text;
	}

	private sealed class TestOptionsProvider : AnalyzerConfigOptionsProvider
	{
		private readonly IReadOnlyDictionary<string, string> _metadata;
		private readonly ISet<string> _externalFiles;
		private readonly AnalyzerConfigOptions _globalOptions;

		public override AnalyzerConfigOptions GlobalOptions => _globalOptions;

		public TestOptionsProvider(
			IReadOnlyDictionary<string, string> metadata,
			string projectCatalogName,
			ISet<string>? externalFiles = null)
		{
			_metadata = metadata;
			_externalFiles = externalFiles ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			_globalOptions = new TestOptions(new Dictionary<string, string>
			{
				["build_property.UmbrellaDynamicImageCatalogName"] = projectCatalogName,
				["build_property.MSBuildProjectName"] = projectCatalogName
			});
		}

		public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => TestOptions.Empty;

		public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
			=> _metadata.TryGetValue(textFile.Path, out string? catalogName)
				? new TestOptions(new Dictionary<string, string>
				{
					["build_metadata.AdditionalFiles.UmbrellaDynamicImageCatalogName"] = catalogName,
					["build_metadata.AdditionalFiles.UmbrellaDynamicImageExternalSource"] = _externalFiles.Contains(textFile.Path).ToString()
				})
				: TestOptions.Empty;
	}

	private sealed class DuplicatePathOptionsProvider : AnalyzerConfigOptionsProvider
	{
		private readonly AdditionalText _first;
		private readonly AdditionalText _second;
		private readonly string _firstCatalog;
		private readonly string _secondCatalog;

		public override AnalyzerConfigOptions GlobalOptions { get; } = new TestOptions(new Dictionary<string, string>
		{
			["build_property.UmbrellaDynamicImageCatalogName"] = "Server"
		});

		public DuplicatePathOptionsProvider(AdditionalText first, string firstCatalog, AdditionalText second, string secondCatalog)
		{
			_first = first;
			_second = second;
			_firstCatalog = firstCatalog;
			_secondCatalog = secondCatalog;
		}

		public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => TestOptions.Empty;

		public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
			=> ReferenceEquals(textFile, _first)
				? new TestOptions(new Dictionary<string, string> { ["build_metadata.AdditionalFiles.UmbrellaDynamicImageCatalogName"] = _firstCatalog })
				: ReferenceEquals(textFile, _second)
					? new TestOptions(new Dictionary<string, string> { ["build_metadata.AdditionalFiles.UmbrellaDynamicImageCatalogName"] = _secondCatalog })
					: TestOptions.Empty;
	}

	private sealed class TestOptions : AnalyzerConfigOptions
	{
		private readonly IReadOnlyDictionary<string, string> _values;
		public static TestOptions Empty { get; } = new(new Dictionary<string, string>());

		public TestOptions(IReadOnlyDictionary<string, string> values) => _values = values;

		public override bool TryGetValue(string key, out string value)
		{
			if (_values.TryGetValue(key, out string? result))
			{
				value = result;
				return true;
			}

			value = string.Empty;
			return false;
		}
	}
}
