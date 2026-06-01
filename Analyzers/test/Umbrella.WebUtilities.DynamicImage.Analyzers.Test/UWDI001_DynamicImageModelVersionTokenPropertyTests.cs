namespace Umbrella.WebUtilities.DynamicImage.Analyzers.Test;

public class UWDI001_DynamicImageModelVersionTokenPropertyTests : AnalyzerTestBase<Umbrella.WebUtilities.DynamicImage.Analyzers.DynamicImageVersioningAnalyzer>
{
	[Fact]
	public async Task DynamicImageUrlProperty_WithoutMatchingVersionTokenProperty_ShouldTriggerDiagnostic()
	{
		const string source = """
public record ProductModel
{
    public string? ImageUrl { get; init; }
}
""" + ExplicitlyEnabledRegistrationSource;

		var expected = Diagnostic(
			Umbrella.WebUtilities.DynamicImage.Analyzers.DynamicImageVersioningAnalyzer.MissingVersionTokenPropertyRule,
			3,
			20,
			"ImageUrl",
			"ProductModel",
			"ImageVersionToken");

		await VerifyAnalyzerAsync(source, expected);
	}

	[Fact]
	public async Task DynamicImageUrlProperty_WithMatchingVersionTokenProperty_ShouldNotTriggerDiagnostic()
	{
		const string source = """
public record ProductModel
{
    public string? ImageUrl { get; init; }
    public string? ImageVersionToken { get; init; }
}
""" + ExplicitlyEnabledRegistrationSource;

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task NonDynamicImageUrlProperty_ShouldNotTriggerDiagnostic()
	{
		const string source = """
public record ProductModel
{
    public string? FileUrl { get; init; }
}
""" + ExplicitlyEnabledRegistrationSource;

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task DefaultEnabledRegistrationWithoutExplicitTrue_ShouldNotTriggerDiagnostic()
	{
		const string source = """
public record ProductModel
{
    public string? ImageUrl { get; init; }
}
""" + DefaultRegistrationSource;

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

	private const string DefaultRegistrationSource = """

public static class Registration
{
    public static void Configure(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
    {
        Microsoft.Extensions.DependencyInjection.DynamicImageServiceCollectionExtensions.AddUmbrellaWebUtilitiesDynamicImage(services);
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
