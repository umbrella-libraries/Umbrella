namespace Umbrella.WebUtilities.DynamicImage.Analyzers.Test;

public class UWDI001To003_DynamicImageActivationTests : AnalyzerTestBase<Umbrella.WebUtilities.DynamicImage.Analyzers.DynamicImageVersioningAnalyzer>
{
	[Fact]
	public async Task VersioningRules_WithoutFingerprintingRegistration_ShouldNotTriggerDiagnostics()
	{
		await VerifyNoDiagnosticsAsync(VersioningViolationsSource);
	}

	[Fact]
	public async Task VersioningRules_WithFingerprintingExplicitlyDisabled_ShouldNotTriggerDiagnostics()
	{
		await VerifyNoDiagnosticsAsync(VersioningViolationsSource + ExplicitlyDisabledRegistrationSource);
	}

	private const string VersioningViolationsSource = """
public record MissingVersionTokenModel
{
    public string? ImageUrl { get; init; }
}

public record IncompleteAssignmentModel
{
    public string? ImageUrl { get; init; }
    public string? ImageVersionToken { get; init; }
}

public static class ModelFactory
{
    public static IncompleteAssignmentModel Create()
        => new()
        {
            ImageUrl = "/images/product.jpg"
        };

    public static void Build(
        Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder,
        IncompleteAssignmentModel model)
    {
        builder.OpenComponent<Umbrella.AspNetCore.Blazor.Components.DynamicImage.UmbrellaDynamicImage>(0);
        builder.AddAttribute(1, "Url", model.ImageUrl);
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
