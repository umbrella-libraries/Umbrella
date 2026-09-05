
using CommunityToolkit.Diagnostics;
using Umbrella.AspNetCore.WebUtilities.DynamicImage;
using Umbrella.AspNetCore.WebUtilities.DynamicImage.Mvc.TagHelpers.Options;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods used to register services for the <see cref="Umbrella.AspNetCore.WebUtilities.DynamicImage"/> package with a specified
/// <see cref="IServiceCollection"/> dependency injection container builder.
/// </summary>
public static class IServiceCollectionExtensions
{
	/// <summary>
	/// Adds the <see cref="Umbrella.AspNetCore.WebUtilities.DynamicImage"/> services to the specified <see cref="IServiceCollection"/> dependency injection container builder.
	/// </summary>
	/// <param name="services">The services dependency injection container builder to which the services will be added.</param>
	/// <param name="dynamicImageTagHelperOptionsBuilder">The dynamic image tag helper options builder.</param>
	/// <param name="focalPointSigningOptionsBuilder">Optional server-only signing configuration for explicit focal points.</param>
	/// <returns>The <see cref="IServiceCollection"/> dependency injection container builder.</returns>
	public static IServiceCollection AddUmbrellaAspNetCoreWebUtilitiesDynamicImage(
		this IServiceCollection services,
		Action<IServiceProvider, DynamicImageTagHelperOptions>? dynamicImageTagHelperOptionsBuilder = null,
		Action<IServiceProvider, DynamicImageFocalPointSigningOptions>? focalPointSigningOptionsBuilder = null)
	{
		Guard.IsNotNull(services, nameof(services));

		_ = services.ConfigureUmbrellaOptions(dynamicImageTagHelperOptionsBuilder);
		_ = services.ConfigureUmbrellaOptions(focalPointSigningOptionsBuilder);
		_ = services.AddSingleton<DynamicImageFocalPointApprovalService>();
		_ = services.AddSingleton<IDynamicImageDescriptorFactory>(provider => provider.GetRequiredService<DynamicImageFocalPointApprovalService>());

		return services;
	}
}
