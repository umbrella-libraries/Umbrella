namespace Umbrella.WebUtilities.DynamicImage.Analyzers.Test;

public class UWDI004_DynamicImageGeneratedVariantCoverageTests : AnalyzerTestBase<Umbrella.WebUtilities.DynamicImage.Analyzers.DynamicImageVersioningAnalyzer>
{
	[Fact]
	public async Task BlazorComponent_WithNonStaticVariantInputs_ShouldTriggerDiagnostic()
	{
		const string source = """
public sealed class ProductModel
{
    public string? ImageUrl { get; set; }
}

public static class RenderFragmentFactory
{
    public static void Build(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder, ProductModel item, int widthRequest, string sizeWidths)
    {
        builder.OpenComponent<Umbrella.AspNetCore.Blazor.Components.DynamicImage.UmbrellaDynamicImage>(0);
        builder.AddAttribute(1, "Url", item.ImageUrl);
        builder.AddAttribute(2, "WidthRequest", widthRequest);
        builder.AddAttribute(3, "HeightRequest", 100);
        builder.AddAttribute(4, "SizeWidths", sizeWidths);
        builder.CloseComponent();
    }
}
""" + SharedBlazorInfrastructureSource;

		var expected = Diagnostic(
			Umbrella.WebUtilities.DynamicImage.Analyzers.DynamicImageVersioningAnalyzer.NonStaticVariantShapingInputRule,
			12,
			17,
			"WidthRequest, SizeWidths");

		await VerifyAnalyzerAsync(source, expected);
	}

	[Fact]
	public async Task BlazorComponent_WithNonStaticVariantInputsAndFingerprintingDisabled_ShouldTriggerDiagnostic()
	{
		const string source = """
public static class RenderFragmentFactory
{
    public static void Build(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder, int widthRequest)
    {
        builder.OpenComponent<Umbrella.AspNetCore.Blazor.Components.DynamicImage.UmbrellaDynamicImage>(0);
        builder.AddAttribute(1, "Url", "/images/product.jpg");
        builder.AddAttribute(2, "WidthRequest", widthRequest);
        builder.CloseComponent();
    }
}
""" + SharedBlazorInfrastructureSource + ExplicitlyDisabledRegistrationSource;

		var expected = Diagnostic(
			Umbrella.WebUtilities.DynamicImage.Analyzers.DynamicImageVersioningAnalyzer.NonStaticVariantShapingInputRule,
			7,
			17,
			"WidthRequest");

		await VerifyAnalyzerAsync(source, expected);
	}

	[Fact]
	public async Task BlazorComponent_WithNonStaticVariantInputsAndFingerprintingEnabled_ShouldTriggerDiagnostic()
	{
		const string source = """
public static class RenderFragmentFactory
{
    public static void Build(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder, int widthRequest)
    {
        builder.OpenComponent<Umbrella.AspNetCore.Blazor.Components.DynamicImage.UmbrellaDynamicImage>(0);
        builder.AddAttribute(1, "Url", "/images/product.jpg");
        builder.AddAttribute(2, "WidthRequest", widthRequest);
        builder.CloseComponent();
    }
}
""" + SharedBlazorInfrastructureSource + ExplicitlyEnabledRegistrationSource;

		var expected = Diagnostic(
			Umbrella.WebUtilities.DynamicImage.Analyzers.DynamicImageVersioningAnalyzer.NonStaticVariantShapingInputRule,
			7,
			17,
			"WidthRequest");

		await VerifyAnalyzerAsync(source, expected);
	}

	[Fact]
	public async Task BlazorComponent_UsingNet10GeneratedComponentParameterShape_ShouldTriggerDiagnostic()
	{
		const string source = """
public static class RenderFragmentFactory
{
    public static void Build(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder, int widthRequest)
    {
        builder.OpenComponent<Umbrella.AspNetCore.Blazor.Components.DynamicImage.UmbrellaDynamicImage>(0);
        builder.AddComponentParameter(1, nameof(Umbrella.AspNetCore.Blazor.Components.DynamicImage.UmbrellaDynamicImage.Url), "/images/product.jpg");
        builder.AddComponentParameter(2, nameof(Umbrella.AspNetCore.Blazor.Components.DynamicImage.UmbrellaDynamicImage.WidthRequest), Microsoft.AspNetCore.Components.CompilerServices.RuntimeHelpers.TypeCheck<int>(widthRequest));
        builder.CloseComponent();
    }
}
""" + SharedBlazorInfrastructureSource;

		var expected = Diagnostic(
			Umbrella.WebUtilities.DynamicImage.Analyzers.DynamicImageVersioningAnalyzer.NonStaticVariantShapingInputRule,
			7,
			121,
			"WidthRequest");

		await VerifyAnalyzerAsync(source, expected);
	}

	[Fact]
	public async Task BlazorComponent_WithStaticVariantInputs_ShouldNotTriggerDiagnostic()
	{
		const string source = """
public static class RenderFragmentFactory
{
    public static void Build(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
    {
        const int widthRequest = 200;
        const string sizeWidths = "100,200";

        builder.OpenComponent<Umbrella.AspNetCore.Blazor.Components.DynamicImage.UmbrellaDynamicImage>(0);
        builder.AddAttribute(1, "Url", "/images/product.jpg");
        builder.AddAttribute(2, "WidthRequest", widthRequest);
        builder.AddAttribute(3, "HeightRequest", 100);
        builder.AddAttribute(4, "SizeWidths", sizeWidths);
        builder.CloseComponent();
    }
}
""" + SharedBlazorInfrastructureSource;

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task BlazorComponent_WithStaticHttpUrl_ShouldNotTriggerDiagnostic()
	{
		const string source = """
public static class RenderFragmentFactory
{
    public static void Build(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder, int widthRequest)
    {
        builder.OpenComponent<Umbrella.AspNetCore.Blazor.Components.DynamicImage.UmbrellaDynamicImage>(0);
        builder.AddAttribute(1, "Url", "https://cdn.example.com/product.jpg");
        builder.AddAttribute(2, "WidthRequest", widthRequest);
        builder.AddAttribute(3, "HeightRequest", 100);
        builder.CloseComponent();
    }
}
""" + SharedBlazorInfrastructureSource;

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task TagHelper_WithNonStaticVariantInputs_ShouldTriggerDiagnostic()
	{
		const string source = """
public record ProductModel
{
    public string? ImageUrl { get; init; }
}

public static class ViewRenderer
{
    public static void Render(ProductModel item, int widthRequest, int density, string sizeWidths)
    {
        var tagHelper = new Umbrella.AspNetCore.WebUtilities.DynamicImage.Mvc.TagHelpers.DynamicImageTagHelper();
        tagHelper.Src = item.ImageUrl;
        tagHelper.WidthRequest = widthRequest;
        tagHelper.HeightRequest = 100;
        tagHelper.ImageMaxPixelDensity = density;
        tagHelper.SizeWidths = sizeWidths;
    }
}
""" + SharedTagHelperInfrastructureSource;

		var expected = Diagnostic(
			Umbrella.WebUtilities.DynamicImage.Analyzers.DynamicImageVersioningAnalyzer.NonStaticVariantShapingInputRule,
			12,
			19,
			"WidthRequest, ImageMaxPixelDensity, SizeWidths");

		await VerifyAnalyzerAsync(source, expected);
	}

	[Fact]
	public async Task TagHelper_WithStaticVariantInputs_ShouldNotTriggerDiagnostic()
	{
		const string source = """
public static class ViewRenderer
{
    public static void Render()
    {
        const int widthRequest = 200;
        const int density = 2;
        const string sizeWidths = "100,200";

        var tagHelper = new Umbrella.AspNetCore.WebUtilities.DynamicImage.Mvc.TagHelpers.DynamicImageTagHelper();
        tagHelper.Src = "/images/product.jpg";
        tagHelper.WidthRequest = widthRequest;
        tagHelper.HeightRequest = 100;
        tagHelper.ImageMaxPixelDensity = density;
        tagHelper.SizeWidths = sizeWidths;
    }
}
""" + SharedTagHelperInfrastructureSource;

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task RazorComponent_WithLiteralAndEnumInputs_ShouldNotTriggerDiagnostic()
	{
		const string razor = """
@using Umbrella.AspNetCore.Blazor.Components.DynamicImage
<UmbrellaDynamicImage WidthRequest="200"
                      HeightRequest="100"
                      MaxPixelDensity="1"
                      ResizeMode="DynamicResizeMode.Crop"
                      ImageFormat="DynamicImageFormat.WebP" />
""";

		await VerifyAnalyzerWithAdditionalFilesAsync(
			SharedBlazorInfrastructureSource,
			[("C:/app/Test.razor", razor)]);
	}

	[Fact]
	public async Task RazorComponent_WithUsingStaticEnumInputs_ShouldNotTriggerDiagnostic()
	{
		const string imports = """
@using Umbrella.AspNetCore.Blazor.Components.DynamicImage
@using Umbrella.DynamicImage.Abstractions
@using    static    DynamicResizeMode
@using static global::Umbrella.DynamicImage.Abstractions.DynamicImageFormat;
""";
		const string razor = """
<UmbrellaDynamicImage WidthRequest="200"
                      HeightRequest="100"
                      MaxPixelDensity="1"
                      ResizeMode="Crop"
                      ImageFormat="Png" />
""";

		await VerifyAnalyzerWithAdditionalFilesAsync(
			SharedBlazorInfrastructureSource,
			[
				("C:/app/_Imports.razor", imports),
				("C:/app/Test.razor", razor)
			]);
	}

	[Fact]
	public async Task RazorComponent_WithOnlyMatchingResizeModeStaticImport_ShouldReportImageFormat()
	{
		const string imports = """
@using Umbrella.AspNetCore.Blazor.Components.DynamicImage
@using Umbrella.DynamicImage.Abstractions
@using static DynamicResizeMode
""";
		const string razor = """
<UmbrellaDynamicImage WidthRequest="200"
                      HeightRequest="100"
                      MaxPixelDensity="1"
                      ResizeMode="Crop"
                      ImageFormat="Png" />
""";

		var expected = Diagnostic(
			Umbrella.WebUtilities.DynamicImage.Analyzers.DynamicImageVersioningAnalyzer.NonStaticVariantShapingInputRule,
			5,
			23,
			"ImageFormat");

		await VerifyAnalyzerWithAdditionalFilesAsync(
			SharedBlazorInfrastructureSource,
			[
				("C:/app/_Imports.razor", imports),
				("C:/app/Test.razor", razor)
			],
			expected);
	}

	[Fact]
	public async Task RazorComponent_WithRuntimeFocalPointInputs_ShouldNotTriggerDiagnostic()
	{
		const string razor = """
@using Umbrella.AspNetCore.Blazor.Components.DynamicImage
<UmbrellaDynamicImage WidthRequest="200"
                      HeightRequest="100"
                      ResizeMode="DynamicResizeMode.CropFocalPoint"
                      FocalPointX="@Model.ImageFocalPointX"
                      FocalPointY="@Model.ImageFocalPointY" />
""";

		await VerifyAnalyzerWithAdditionalFilesAsync(
			SharedBlazorInfrastructureSource,
			[("C:/app/Test.razor", razor)]);
	}

	[Fact]
	public async Task RazorFileImagePreviewUpload_WithNonStaticVariantInput_ShouldTriggerDiagnostic()
	{
		const string razor = """
@using Umbrella.AspNetCore.Blazor.Components.FileImagePreviewUpload
<UmbrellaFileImagePreviewUpload Url="@Model.ImageUrl"
                                VersionToken="@Model.ImageVersionToken"
                                WidthRequest="@Model.Width"
                                HeightRequest="100" />
""";

		var expected = Diagnostic(
			Umbrella.WebUtilities.DynamicImage.Analyzers.DynamicImageVersioningAnalyzer.NonStaticVariantShapingInputRule,
			4,
			33,
			"WidthRequest");

		await VerifyAnalyzerWithAdditionalFilesAsync(
			SharedBlazorInfrastructureSource,
			[("C:/app/Test.razor", razor)],
			expected);
	}

	[Fact]
	public async Task RazorFileImagePreviewUpload_WithRuntimeFocalPointInputs_ShouldNotTriggerDiagnostic()
	{
		const string razor = """
@using Umbrella.AspNetCore.Blazor.Components.FileImagePreviewUpload
<UmbrellaFileImagePreviewUpload WidthRequest="200"
                                HeightRequest="100"
                                ResizeMode="DynamicResizeMode.CropFocalPoint"
                                EnableFocalPointSelection="true"
                                FocalPointX="@Model.ImageFocalPointX"
                                FocalPointY="@Model.ImageFocalPointY" />
""";

		await VerifyAnalyzerWithAdditionalFilesAsync(
			SharedBlazorInfrastructureSource,
			[("C:/app/Test.razor", razor)]);
	}

	[Fact]
	public async Task RazorFileImagePreviewUpload_WithRuntimeSelectionFlag_ShouldTriggerDiagnostic()
	{
		const string razor = """
@using Umbrella.AspNetCore.Blazor.Components.FileImagePreviewUpload
<UmbrellaFileImagePreviewUpload WidthRequest="200"
                                HeightRequest="100"
                                ResizeMode="DynamicResizeMode.CropFocalPoint"
                                EnableFocalPointSelection="@Model.EnableFocalPointSelection" />
""";

		var expected = Diagnostic(
			Umbrella.WebUtilities.DynamicImage.Analyzers.DynamicImageVersioningAnalyzer.NonStaticVariantShapingInputRule,
			5,
			33,
			"EnableFocalPointSelection");

		await VerifyAnalyzerWithAdditionalFilesAsync(
			SharedBlazorInfrastructureSource,
			[("C:/app/Test.razor", razor)],
			expected);
	}

	[Fact]
	public async Task FileImagePreviewRenderTree_WithRuntimeSelectionFlag_ShouldTriggerDiagnostic()
	{
		const string source = """
public static class RenderFragmentFactory
{
    public static void Build(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder, bool enableFocalPointSelection)
    {
        builder.OpenComponent<Umbrella.AspNetCore.Blazor.Components.FileImagePreviewUpload.UmbrellaFileImagePreviewUpload>(0);
        builder.AddAttribute(1, "Url", "/images/product.jpg");
        builder.AddAttribute(2, "WidthRequest", 200);
        builder.AddAttribute(3, "HeightRequest", 100);
        builder.AddAttribute(4, "EnableFocalPointSelection", enableFocalPointSelection);
        builder.CloseComponent();
    }
}
""" + SharedBlazorInfrastructureSource;

		var expected = Diagnostic(
			Umbrella.WebUtilities.DynamicImage.Analyzers.DynamicImageVersioningAnalyzer.NonStaticVariantShapingInputRule,
			9,
			17,
			"EnableFocalPointSelection");

		await VerifyAnalyzerAsync(source, expected);
	}

	[Fact]
	public async Task RazorComponent_WithConstantReference_ShouldTriggerDiagnostic()
	{
		const string razor = """
@using Umbrella.AspNetCore.Blazor.Components.DynamicImage
<UmbrellaDynamicImage WidthRequest="@CardWidth"
                      HeightRequest="100" />
""";

		var expected = Diagnostic(
			Umbrella.WebUtilities.DynamicImage.Analyzers.DynamicImageVersioningAnalyzer.NonStaticVariantShapingInputRule,
			2,
			23,
			"WidthRequest");

		await VerifyAnalyzerWithAdditionalFilesAsync(
			SharedBlazorInfrastructureSource,
			[("C:/app/Test.razor", razor)],
			expected);
	}

	[Fact]
	public async Task PreparedExternalRazorComponent_WithNonStaticInput_ShouldTriggerDiagnostic()
	{
		const string razor = """
<UmbrellaDynamicImage WidthRequest="@Model.Width"
                      HeightRequest="100" />
""";

		var expected = Diagnostic(
			Umbrella.WebUtilities.DynamicImage.Analyzers.DynamicImageVersioningAnalyzer.NonStaticVariantShapingInputRule,
			1,
			23,
			"WidthRequest");

		await VerifyAnalyzerWithAdditionalFilesAsync(
			SharedBlazorInfrastructureSource,
			[
				("C:/app/_Imports.razor.umbrella-dynamic-image", "@using Umbrella.AspNetCore.Blazor.Components.DynamicImage"),
				("C:/app/Test.razor.umbrella-dynamic-image", razor)
			],
			expected);
	}

	[Fact]
	public async Task RazorComponent_WithDynamicEnumBinding_ShouldTriggerDiagnostic()
	{
		const string razor = """
@using Umbrella.AspNetCore.Blazor.Components.DynamicImage
<UmbrellaDynamicImage WidthRequest="200"
                      HeightRequest="100"
                      MaxPixelDensity="1"
                      ResizeMode="@Model.ResizeMode" />
""";

		var expected = Diagnostic(
			Umbrella.WebUtilities.DynamicImage.Analyzers.DynamicImageVersioningAnalyzer.NonStaticVariantShapingInputRule,
			5,
			23,
			"ResizeMode");

		await VerifyAnalyzerWithAdditionalFilesAsync(
			SharedBlazorInfrastructureSource,
			[("C:/app/Test.razor", razor)],
			expected);
	}

	[Fact]
	public async Task RazorTagHelper_WithMixedStringExpression_ShouldTriggerDiagnostic()
	{
		const string viewImports = "@addTagHelper *, Umbrella.AspNetCore.WebUtilities.DynamicImage";
		const string view = """
<dynamic-image src="/images/test.jpg"
               width-request="200"
               height-request="100"
               image-density="1"
               size-widths="100,@Model.Width" />
""";

		var expected = Diagnostic(
			Umbrella.WebUtilities.DynamicImage.Analyzers.DynamicImageVersioningAnalyzer.NonStaticVariantShapingInputRule,
			5,
			16,
			"SizeWidths");

		await VerifyAnalyzerWithAdditionalFilesAsync(
			SharedTagHelperInfrastructureSource,
			[
				("C:/app/Views/_ViewImports.cshtml", viewImports),
				("C:/app/Views/Test.cshtml", view)
			],
			expected);
	}

	[Fact]
	public async Task RazorTagHelper_WithRuntimeFocalPointInputs_ShouldNotTriggerDiagnostic()
	{
		const string viewImports = "@addTagHelper *, Umbrella.AspNetCore.WebUtilities.DynamicImage";
		const string view = """
<dynamic-image src="/images/test.jpg"
               width-request="200"
               height-request="100"
               resize-mode="CropFocalPoint"
               focal-point-x="@Model.ImageFocalPointX"
               focal-point-y="@Model.ImageFocalPointY" />
""";

		await VerifyAnalyzerWithAdditionalFilesAsync(
			SharedTagHelperInfrastructureSource,
			[
				("C:/app/Views/_ViewImports.cshtml", viewImports),
				("C:/app/Views/Test.cshtml", view)
			]);
	}

	[Fact]
	public async Task RazorTagHelper_RemovingPictureSourceOnlyLeavesDynamicImageActive()
	{
		const string viewImports = """
@addTagHelper *, Umbrella.AspNetCore.WebUtilities.DynamicImage
@removeTagHelper Umbrella.AspNetCore.WebUtilities.DynamicImage.Mvc.TagHelpers.DynamicImagePictureSourceTagHelper, Umbrella.AspNetCore.WebUtilities.DynamicImage
""";
		const string view = """
<dynamic-image src="/images/test.jpg"
               width-request="@Model.Width"
               height-request="100" />
""";

		var expected = Diagnostic(
			Umbrella.WebUtilities.DynamicImage.Analyzers.DynamicImageVersioningAnalyzer.NonStaticVariantShapingInputRule,
			2,
			16,
			"WidthRequest");

		await VerifyAnalyzerWithAdditionalFilesAsync(
			SharedTagHelperInfrastructureSource,
			[
				("C:/app/Views/_ViewImports.cshtml", viewImports),
				("C:/app/Views/Test.cshtml", view)
			],
			expected);
	}

	[Fact]
	public async Task RazorTagHelper_UnrelatedTypeDirectiveDoesNotActivateDynamicImage()
	{
		const string viewImports =
			"@addTagHelper Umbrella.AspNetCore.WebUtilities.DynamicImage.Mvc.TagHelpers.UnrelatedTagHelper, Umbrella.AspNetCore.WebUtilities.DynamicImage";
		const string view = """
<dynamic-image src="/images/test.jpg"
               width-request="@Model.Width"
               height-request="100" />
""";

		await VerifyAnalyzerWithAdditionalFilesAsync(
			SharedTagHelperInfrastructureSource,
			[
				("C:/app/Views/_ViewImports.cshtml", viewImports),
				("C:/app/Views/Test.cshtml", view)
			]);
	}

	private const string SharedBlazorInfrastructureSource = """

namespace Microsoft.AspNetCore.Components.Rendering
{
    public class RenderTreeBuilder
    {
        public void OpenComponent<TComponent>(int sequence) { }
        public void AddAttribute(int sequence, string name, object? value) { }
        public void AddComponentParameter(int sequence, string name, object? value) { }
        public void CloseComponent() { }
    }
}

namespace Microsoft.AspNetCore.Components.CompilerServices
{
    public static class RuntimeHelpers
    {
        public static T TypeCheck<T>(T value) => value;
    }
}

namespace Umbrella.AspNetCore.Blazor.Components.DynamicImage
{
    public class UmbrellaDynamicImage
    {
        public string? Url { get; set; }
        public int WidthRequest { get; set; }
    }
}

namespace Umbrella.AspNetCore.Blazor.Components.FileImagePreviewUpload
{
	public class UmbrellaFileImagePreviewUpload
	{
        public string? Url { get; set; }
        public string? VersionToken { get; set; }
		public int WidthRequest { get; set; }
		public bool EnableFocalPointSelection { get; set; }
		public double? FocalPointX { get; set; }
		public double? FocalPointY { get; set; }
	}
}
""";

	private const string SharedTagHelperInfrastructureSource = """

namespace Umbrella.AspNetCore.WebUtilities.DynamicImage.Mvc.TagHelpers
{
    public class DynamicImageTagHelperBase
    {
        public string? Src { get; set; }
        public string? VersionToken { get; set; }
        public int WidthRequest { get; set; }
        public int HeightRequest { get; set; }
        public int ImageMaxPixelDensity { get; set; }
        public string? SizeWidths { get; set; }
    }

    public sealed class DynamicImageTagHelper : DynamicImageTagHelperBase
    {
    }

    public sealed class DynamicImagePictureSourceTagHelper : DynamicImageTagHelperBase
    {
    }
}

""";

	private const string ExplicitlyDisabledRegistrationSource = """

public static class Registration
{
    public static void Configure(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
    {
        Microsoft.Extensions.DependencyInjection.DynamicImageServiceCollectionExtensions.AddUmbrellaWebUtilitiesDynamicImage(
            services,
            (serviceProvider, options) => options.EnableUrlFingerprinting = false);
    }
}
""" + RegistrationInfrastructureSource;

	private const string ExplicitlyEnabledRegistrationSource = """

public static class Registration
{
    public static void Configure(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
    {
        Microsoft.Extensions.DependencyInjection.DynamicImageServiceCollectionExtensions.AddUmbrellaWebUtilitiesDynamicImage(
            services,
            (serviceProvider, options) => options.EnableUrlFingerprinting = true);
    }
}
""" + RegistrationInfrastructureSource;

	private const string RegistrationInfrastructureSource = """

namespace Microsoft.Extensions.DependencyInjection
{
    public interface IServiceCollection
    {
    }

    public static class DynamicImageServiceCollectionExtensions
    {
        public static IServiceCollection AddUmbrellaWebUtilitiesDynamicImage(
            IServiceCollection services,
            System.Action<System.IServiceProvider, Umbrella.WebUtilities.DynamicImage.Middleware.Options.DynamicImageMiddlewareOptions>? optionsBuilder = null)
            => services;
    }
}

namespace Umbrella.WebUtilities.DynamicImage.Middleware.Options
{
    public class DynamicImageMiddlewareOptions
    {
        public bool EnableUrlFingerprinting { get; set; }
    }
}
""";
}
