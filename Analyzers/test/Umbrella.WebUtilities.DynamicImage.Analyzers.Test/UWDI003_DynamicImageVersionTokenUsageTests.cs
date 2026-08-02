namespace Umbrella.WebUtilities.DynamicImage.Analyzers.Test;

public class UWDI003_DynamicImageVersionTokenUsageTests : AnalyzerTestBase<Umbrella.WebUtilities.DynamicImage.Analyzers.DynamicImageVersioningAnalyzer>
{
	[Fact]
	public async Task BlazorComponentUsage_WithoutVersionToken_ShouldTriggerDiagnostic()
	{
		const string source = """
public record ProductModel
{
    public string? ImageUrl { get; init; }
    public string? ImageVersionToken { get; init; }
}

public static class RenderFragmentFactory
{
    public static void Build(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder, ProductModel item)
    {
        builder.OpenComponent<Umbrella.AspNetCore.Blazor.Components.DynamicImage.UmbrellaDynamicImage>(0);
        builder.AddAttribute(1, "Url", Microsoft.AspNetCore.Components.CompilerServices.RuntimeHelpers.TypeCheck<string?>(item.ImageUrl));
        builder.CloseComponent();
    }
}

namespace Microsoft.AspNetCore.Components.Rendering
{
    public class RenderTreeBuilder
    {
        public void OpenComponent<TComponent>(int sequence) { }
        public void AddAttribute(int sequence, string name, object? value) { }
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
    }
}
""" + ExplicitlyEnabledRegistrationSource;

		var expected = Diagnostic(
			Umbrella.WebUtilities.DynamicImage.Analyzers.DynamicImageVersioningAnalyzer.MissingVersionTokenUsageRule,
			12,
			17,
			"ImageUrl",
			"ImageVersionToken");

		await VerifyAnalyzerAsync(source, expected);
	}

	[Fact]
	public async Task BlazorComponentUsage_WithVersionToken_ShouldNotTriggerDiagnostic()
	{
		const string source = """
public record ProductModel
{
    public string? ImageUrl { get; init; }
    public string? ImageVersionToken { get; init; }
}

public static class RenderFragmentFactory
{
    public static void Build(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder, ProductModel item)
    {
        builder.OpenComponent<Umbrella.AspNetCore.Blazor.Components.DynamicImage.UmbrellaDynamicImage>(0);
        builder.AddAttribute(1, "Url", item.ImageUrl);
        builder.AddAttribute(2, "VersionToken", item.ImageVersionToken);
        builder.CloseComponent();
    }
}

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
""" + ExplicitlyEnabledRegistrationSource;

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task FileImagePreviewUploadUsage_WithoutVersionToken_ShouldTriggerDiagnostic()
	{
		const string source = """
public record ProductModel
{
    public string? ImageUrl { get; init; }
    public string? ImageVersionToken { get; init; }
}

public static class RenderFragmentFactory
{
    public static void Build(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder, ProductModel item)
    {
        builder.OpenComponent<Umbrella.AspNetCore.Blazor.Components.FileImagePreviewUpload.UmbrellaFileImagePreviewUpload>(0);
        builder.AddAttribute(1, "Url", item.ImageUrl);
        builder.CloseComponent();
    }
}

namespace Microsoft.AspNetCore.Components.Rendering
{
    public class RenderTreeBuilder
    {
        public void OpenComponent<TComponent>(int sequence) { }
        public void AddAttribute(int sequence, string name, object? value) { }
        public void CloseComponent() { }
    }
}

namespace Umbrella.AspNetCore.Blazor.Components.FileImagePreviewUpload
{
    public class UmbrellaFileImagePreviewUpload { }
}
""" + ExplicitlyEnabledRegistrationSource;

		var expected = Diagnostic(
			Umbrella.WebUtilities.DynamicImage.Analyzers.DynamicImageVersioningAnalyzer.MissingVersionTokenUsageRule,
			12,
			17,
			"ImageUrl",
			"ImageVersionToken");

		await VerifyAnalyzerAsync(source, expected);
	}

	[Fact]
	public async Task FileImagePreviewUploadUsage_WithVersionToken_ShouldNotTriggerDiagnostic()
	{
		const string source = """
public record ProductModel
{
    public string? ImageUrl { get; init; }
    public string? ImageVersionToken { get; init; }
}

public static class RenderFragmentFactory
{
    public static void Build(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder, ProductModel item)
    {
        builder.OpenComponent<Umbrella.AspNetCore.Blazor.Components.FileImagePreviewUpload.UmbrellaFileImagePreviewUpload>(0);
        builder.AddAttribute(1, "Url", item.ImageUrl);
        builder.AddAttribute(2, "VersionToken", item.ImageVersionToken);
        builder.CloseComponent();
    }
}

namespace Microsoft.AspNetCore.Components.Rendering
{
    public class RenderTreeBuilder
    {
        public void OpenComponent<TComponent>(int sequence) { }
        public void AddAttribute(int sequence, string name, object? value) { }
        public void CloseComponent() { }
    }
}

namespace Umbrella.AspNetCore.Blazor.Components.FileImagePreviewUpload
{
    public class UmbrellaFileImagePreviewUpload { }
}
""" + ExplicitlyEnabledRegistrationSource;

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task RazorFileImagePreviewUpload_WithNullConditionalModelUrlAndNoVersionToken_ShouldTriggerDiagnostic()
	{
		const string razor = """
@using Umbrella.AspNetCore.Blazor.Components.FileImagePreviewUpload
<UmbrellaFileImagePreviewUpload Url="@Model?.ImageUrl"
                                WidthRequest="400"
                                HeightRequest="400" />
""";

		const string componentSource = """
namespace Umbrella.AspNetCore.Blazor.Components.FileImagePreviewUpload
{
    public class UmbrellaFileImagePreviewUpload { }
}
""";

		var expected = Diagnostic(
			Umbrella.WebUtilities.DynamicImage.Analyzers.DynamicImageVersioningAnalyzer.MissingVersionTokenUsageRule,
			2,
			33,
			"ImageUrl",
			"ImageVersionToken");

		await VerifyAnalyzerWithAdditionalFilesAsync(
			componentSource + ExplicitlyEnabledRegistrationSource,
			[("C:/app/Test.razor", razor)],
			expected);
	}

	[Fact]
	public async Task RazorDynamicImage_WithItemUrlAndNoVersionToken_ShouldTriggerDiagnostic()
	{
		const string razor = """
@using Umbrella.AspNetCore.Blazor.Components.DynamicImage
<UmbrellaDynamicImage Url="@item.ImageUrl"
                      WidthRequest="400"
                      HeightRequest="400" />
""";

		const string componentSource = """
namespace Umbrella.AspNetCore.Blazor.Components.DynamicImage
{
    public class UmbrellaDynamicImage { }
}
""";

		var expected = Diagnostic(
			Umbrella.WebUtilities.DynamicImage.Analyzers.DynamicImageVersioningAnalyzer.MissingVersionTokenUsageRule,
			2,
			23,
			"ImageUrl",
			"ImageVersionToken");

		await VerifyAnalyzerWithAdditionalFilesAsync(
			componentSource + ExplicitlyEnabledRegistrationSource,
			[("C:/app/Test.razor", razor)],
			expected);
	}

	[Fact]
	public async Task RazorComponent_WithMatchingVersionToken_ShouldNotTriggerDiagnostic()
	{
		const string razor = """
@using Umbrella.AspNetCore.Blazor.Components.DynamicImage
<UmbrellaDynamicImage Url="@item.ImageUrl"
                      VersionToken="@item.ImageVersionToken"
                      WidthRequest="400"
                      HeightRequest="400" />
""";

		const string componentSource = """
namespace Umbrella.AspNetCore.Blazor.Components.DynamicImage
{
    public class UmbrellaDynamicImage { }
}
""";

		await VerifyAnalyzerWithAdditionalFilesAsync(
			componentSource + ExplicitlyEnabledRegistrationSource,
			[("C:/app/Test.razor", razor)]);
	}

	[Fact]
	public async Task TagHelperUsage_WithoutVersionToken_ShouldTriggerDiagnostic()
	{
		const string source = """
public record ProductModel
{
    public string? ImageUrl { get; init; }
    public string? ImageVersionToken { get; init; }
}

public static class ViewRenderer
{
    public static void Render(ProductModel item)
    {
        var tagHelper = new Umbrella.AspNetCore.WebUtilities.DynamicImage.Mvc.TagHelpers.DynamicImageTagHelper();
        tagHelper.Src = item.ImageUrl;
    }
}

namespace Umbrella.AspNetCore.WebUtilities.DynamicImage.Mvc.TagHelpers
{
    public class DynamicImageTagHelperBase
    {
        public string? Src { get; set; }
        public string? VersionToken { get; set; }
    }

    public sealed class DynamicImageTagHelper : DynamicImageTagHelperBase
    {
    }
}
""" + ExplicitlyEnabledRegistrationSource;

		var expected = Diagnostic(
			Umbrella.WebUtilities.DynamicImage.Analyzers.DynamicImageVersioningAnalyzer.MissingVersionTokenUsageRule,
			12,
			19,
			"ImageUrl",
			"ImageVersionToken");

		await VerifyAnalyzerAsync(source, expected);
	}

	[Fact]
	public async Task TagHelperUsage_WithVersionToken_ShouldNotTriggerDiagnostic()
	{
		const string source = """
public record ProductModel
{
    public string? ImageUrl { get; init; }
    public string? ImageVersionToken { get; init; }
}

public static class ViewRenderer
{
    public static void Render(ProductModel item)
    {
        var tagHelper = new Umbrella.AspNetCore.WebUtilities.DynamicImage.Mvc.TagHelpers.DynamicImageTagHelper();
        tagHelper.Src = item.ImageUrl;
        tagHelper.VersionToken = item.ImageVersionToken;
    }
}

namespace Umbrella.AspNetCore.WebUtilities.DynamicImage.Mvc.TagHelpers
{
    public class DynamicImageTagHelperBase
    {
        public string? Src { get; set; }
        public string? VersionToken { get; set; }
    }

    public sealed class DynamicImageTagHelper : DynamicImageTagHelperBase
    {
    }
}
""" + ExplicitlyEnabledRegistrationSource;

		await VerifyNoDiagnosticsAsync(source);
	}

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
