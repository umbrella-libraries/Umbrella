
using CommunityToolkit.Diagnostics;
using Umbrella.DynamicImage.Abstractions;
using Umbrella.WebUtilities.DynamicImage.Middleware.Options;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for <see cref="DynamicImageMiddlewareOptions" /> instances.
/// </summary>
public static class DynamicImageMiddlewareOptionsExtensions
{
	/// <summary>
	/// Adds allowed Dynamic Image variants to the middleware options and optionally enables validation.
	/// </summary>
	/// <param name="options">The options.</param>
	/// <param name="variants">The variants to allow, such as a source-generated catalog.</param>
	/// <param name="enableValidation"><see langword="true" /> to enable validation after merging the variants; otherwise <see langword="false" />.</param>
	/// <returns>The same <see cref="DynamicImageMiddlewareOptions" /> instance.</returns>
	/// <remarks>
	/// This is intended for wiring generated catalogs such as
	/// <c>Umbrella.Generated.DynamicImage.UmbrellaDynamicImageComponentVariantCatalog.All</c> into runtime middleware configuration.
	/// Existing <see cref="DynamicImageMiddlewareOptions.AllowedVariants" /> entries are preserved.
	/// </remarks>
	public static DynamicImageMiddlewareOptions AddAllowedVariants(
		this DynamicImageMiddlewareOptions options,
		IEnumerable<DynamicImageVariant> variants,
		bool enableValidation = true)
	{
		Guard.IsNotNull(options);
		Guard.IsNotNull(variants);

		options.AllowedVariants ??= [];

		foreach (DynamicImageVariant variant in variants)
			_ = options.AllowedVariants.Add(variant);

		if (enableValidation)
			options.EnableValidation = true;

		return options;
	}
}
