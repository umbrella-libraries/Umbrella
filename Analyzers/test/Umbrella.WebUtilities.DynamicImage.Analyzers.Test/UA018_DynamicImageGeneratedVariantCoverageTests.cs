namespace Umbrella.WebUtilities.DynamicImage.Analyzers.Test;

public class UA018_DynamicImageGeneratedVariantCoverageTests : AnalyzerTestBase<Umbrella.WebUtilities.DynamicImage.Analyzers.DynamicImageVersioningAnalyzer>
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

	private const string SharedBlazorInfrastructureSource = """

namespace Microsoft.AspNetCore.Components.Rendering
{
    public class RenderTreeBuilder
    {
        public void OpenComponent<TComponent>(int sequence) { }
        public void AddAttribute(int sequence, string name, object? value) { }
        public void CloseComponent() { }
    }
}

namespace Umbrella.AspNetCore.Blazor.Components.DynamicImage
{
    public class UmbrellaDynamicImage
    {
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
}
""";
}
