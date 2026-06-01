namespace Umbrella.WebUtilities.DynamicImage.Analyzers.Test;

public class UWDI002_DynamicImageModelVersionTokenAssignmentTests : AnalyzerTestBase<Umbrella.WebUtilities.DynamicImage.Analyzers.DynamicImageVersioningAnalyzer>
{
	[Fact]
	public async Task ObjectInitializer_WithoutMatchingVersionTokenAssignment_ShouldTriggerDiagnostic()
	{
		const string source = """
public record ProductModel
{
    public string? ImageUrl { get; init; }
    public string? ImageVersionToken { get; init; }
}

public static class Mapper
{
    public static ProductModel Map(string url)
    {
        return new ProductModel
        {
            ImageUrl = url,
        };
    }
}
""" + ExplicitlyEnabledRegistrationSource;

		var expected = Diagnostic(
			Umbrella.WebUtilities.DynamicImage.Analyzers.DynamicImageVersioningAnalyzer.MissingVersionTokenAssignmentRule,
			13,
			13,
			"ImageUrl",
			"ImageVersionToken");

		await VerifyAnalyzerAsync(source, expected);
	}

	[Fact]
	public async Task ObjectInitializer_WithMatchingVersionTokenAssignment_ShouldNotTriggerDiagnostic()
	{
		const string source = """
public record ProductModel
{
    public string? ImageUrl { get; init; }
    public string? ImageVersionToken { get; init; }
}

public static class Mapper
{
    public static ProductModel Map(string url, string token)
    {
        return new ProductModel
        {
            ImageUrl = url,
            ImageVersionToken = token,
        };
    }
}
""" + ExplicitlyEnabledRegistrationSource;

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task AssignmentFlow_WithoutMatchingVersionTokenAssignment_ShouldTriggerDiagnostic()
	{
		const string source = """
public class ProductModel
{
    public string? ImageUrl { get; set; }
    public string? ImageVersionToken { get; set; }
}

public static class Mapper
{
    public static ProductModel Map(string url)
    {
        ProductModel model = new();
        model.ImageUrl = url;

        return model;
    }
}
""" + ExplicitlyEnabledRegistrationSource;

		var expected = Diagnostic(
			Umbrella.WebUtilities.DynamicImage.Analyzers.DynamicImageVersioningAnalyzer.MissingVersionTokenAssignmentRule,
			12,
			15,
			"ImageUrl",
			"ImageVersionToken");

		await VerifyAnalyzerAsync(source, expected);
	}

	[Fact]
	public async Task AssignmentFlow_WithMatchingVersionTokenAssignment_ShouldNotTriggerDiagnostic()
	{
		const string source = """
public class ProductModel
{
    public string? ImageUrl { get; set; }
    public string? ImageVersionToken { get; set; }
}

public static class Mapper
{
    public static ProductModel Map(string url, string token)
    {
        ProductModel model = new();
        model.ImageUrl = url;
        model.ImageVersionToken = token;

        return model;
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
