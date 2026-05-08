using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using Umbrella.DynamicImage.Abstractions;

namespace Umbrella.Generators.DynamicImage.Test;

public class DynamicImageComponentVariantSourceGeneratorTests
{
	[Fact]
	public void GenerateStaticDensityVariantsEmitsExpectedEntries()
	{
		const string source = """
using Umbrella.DynamicImage.Abstractions;

public static class RenderFragmentFactory
{
	public static void Build(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
	{
		builder.OpenComponent<Umbrella.AspNetCore.Blazor.Components.DynamicImage.UmbrellaDynamicImage>(0);
		builder.AddAttribute(1, "Url", "/images/product.jpg");
		builder.AddAttribute(2, "WidthRequest", 100);
		builder.AddAttribute(3, "HeightRequest", 50);
		builder.AddAttribute(4, "MaxPixelDensity", 2);
		builder.AddAttribute(5, "ResizeMode", DynamicResizeMode.CropFocalPoint);
		builder.AddAttribute(6, "ImageFormat", DynamicImageFormat.WebP);
		builder.CloseComponent();
	}
}
""" + SharedComponentInfrastructureSource;

		DynamicImageVariant[] variants = GenerateVariants(source);

		Assert.Equal(
		[
			new DynamicImageVariant(100, 50, DynamicResizeMode.CropFocalPoint, DynamicImageFormat.WebP),
			new DynamicImageVariant(200, 100, DynamicResizeMode.CropFocalPoint, DynamicImageFormat.WebP)
		], variants);
	}

	[Fact]
	public void GenerateStaticSizeWidthsAddsBaseAndResponsiveVariants()
	{
		const string source = """
using Umbrella.DynamicImage.Abstractions;

public static class RenderFragmentFactory
{
	public static void Build(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
	{
		builder.OpenComponent<Umbrella.AspNetCore.Blazor.Components.DynamicImage.UmbrellaDynamicImage>(0);
		builder.AddAttribute(1, "Url", "/images/product.jpg");
		builder.AddAttribute(2, "WidthRequest", 200);
		builder.AddAttribute(3, "HeightRequest", 100);
		builder.AddAttribute(4, "MaxPixelDensity", 2);
		builder.AddAttribute(5, "ImageFormat", DynamicImageFormat.Png);
		builder.AddAttribute(6, "SizeWidths", "100, 200,,abc,100");
		builder.CloseComponent();
	}
}
""" + SharedComponentInfrastructureSource;

		DynamicImageVariant[] variants = GenerateVariants(source);

		Assert.Equal(
		[
			new DynamicImageVariant(100, 50, DynamicResizeMode.Crop, DynamicImageFormat.Png),
			new DynamicImageVariant(200, 100, DynamicResizeMode.Crop, DynamicImageFormat.Png),
			new DynamicImageVariant(400, 200, DynamicResizeMode.Crop, DynamicImageFormat.Png)
		], variants);
	}

	[Fact]
	public void GenerateStaticHttpUrlSkipsVariantEmission()
	{
		const string source = """
public static class RenderFragmentFactory
{
	public static void Build(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
	{
		builder.OpenComponent<Umbrella.AspNetCore.Blazor.Components.DynamicImage.UmbrellaDynamicImage>(0);
		builder.AddAttribute(1, "Url", "https://cdn.example.com/images/product.jpg");
		builder.AddAttribute(2, "WidthRequest", 200);
		builder.AddAttribute(3, "HeightRequest", 100);
		builder.CloseComponent();
	}
}
""" + SharedComponentInfrastructureSource;

		DynamicImageVariant[] variants = GenerateVariants(source);

		Assert.Empty(variants);
	}

	[Fact]
	public void GenerateNonStaticParametersFallBackToComponentDefaults()
	{
		const string source = """
using Umbrella.DynamicImage.Abstractions;

public sealed class ProductModel
{
	public string? ImageUrl { get; set; }
	public int Height { get; set; }
	public DynamicResizeMode ResizeMode { get; set; }
	public DynamicImageFormat ImageFormat { get; set; }
}

public static class RenderFragmentFactory
{
	public static void Build(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder, ProductModel item, int widthRequest, string sizeWidths)
	{
		builder.OpenComponent<Umbrella.AspNetCore.Blazor.Components.DynamicImage.UmbrellaDynamicImage>(0);
		builder.AddAttribute(1, "Url", Microsoft.AspNetCore.Components.CompilerServices.RuntimeHelpers.TypeCheck<string?>(item.ImageUrl));
		builder.AddAttribute(2, "WidthRequest", widthRequest);
		builder.AddAttribute(3, "HeightRequest", item.Height);
		builder.AddAttribute(4, "ResizeMode", Microsoft.AspNetCore.Components.CompilerServices.RuntimeHelpers.TypeCheck<DynamicResizeMode>(item.ResizeMode));
		builder.AddAttribute(5, "ImageFormat", item.ImageFormat);
		builder.AddAttribute(6, "MaxPixelDensity", widthRequest);
		builder.AddAttribute(7, "SizeWidths", sizeWidths);
		builder.CloseComponent();
	}
}
""" + SharedComponentInfrastructureSource;

		DynamicImageVariant[] variants = GenerateVariants(source);

		Assert.Equal(
		[
			new DynamicImageVariant(1, 1, DynamicResizeMode.Crop, DynamicImageFormat.Jpeg),
			new DynamicImageVariant(2, 2, DynamicResizeMode.Crop, DynamicImageFormat.Jpeg),
			new DynamicImageVariant(3, 3, DynamicResizeMode.Crop, DynamicImageFormat.Jpeg)
		], variants);
	}

	[Fact]
	public void SizeWidthsWithNonPositiveValuesFiltersThemOut()
	{
		const string source = """
using Umbrella.DynamicImage.Abstractions;

public static class RenderFragmentFactory
{
	public static void Build(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
	{
		builder.OpenComponent<Umbrella.AspNetCore.Blazor.Components.DynamicImage.UmbrellaDynamicImage>(0);
		builder.AddAttribute(1, "Url", "/images/product.jpg");
		builder.AddAttribute(2, "WidthRequest", 200);
		builder.AddAttribute(3, "HeightRequest", 100);
		builder.AddAttribute(4, "MaxPixelDensity", 1);
		builder.AddAttribute(5, "SizeWidths", "0,-5,100");
		builder.CloseComponent();
	}
}
""" + SharedComponentInfrastructureSource;

		DynamicImageVariant[] variants = GenerateVariants(source);

		// 0 and -5 must be excluded; only sizeWidth=100 is valid
		// base: (200, 100, Crop, Jpeg); size-width: density1 → (100, 50, Crop, Jpeg)
		Assert.Equal(
		[
			new DynamicImageVariant(100, 50, DynamicResizeMode.Crop, DynamicImageFormat.Jpeg),
			new DynamicImageVariant(200, 100, DynamicResizeMode.Crop, DynamicImageFormat.Jpeg)
		], variants);
	}

	[Fact]
	public void SizeWidthsWithOnlyInvalidEntriesFallsBackToDensityVariants()
	{
		const string source = """
using Umbrella.DynamicImage.Abstractions;

public static class RenderFragmentFactory
{
	public static void Build(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
	{
		builder.OpenComponent<Umbrella.AspNetCore.Blazor.Components.DynamicImage.UmbrellaDynamicImage>(0);
		builder.AddAttribute(1, "Url", "/images/product.jpg");
		builder.AddAttribute(2, "WidthRequest", 100);
		builder.AddAttribute(3, "HeightRequest", 50);
		builder.AddAttribute(4, "MaxPixelDensity", 2);
		builder.AddAttribute(5, "SizeWidths", "abc,0,-10");
		builder.CloseComponent();
	}
}
""" + SharedComponentInfrastructureSource;

		DynamicImageVariant[] variants = GenerateVariants(source);

		// All SizeWidths entries are invalid → fall back to density path
		Assert.Equal(
		[
			new DynamicImageVariant(100, 50, DynamicResizeMode.Crop, DynamicImageFormat.Jpeg),
			new DynamicImageVariant(200, 100, DynamicResizeMode.Crop, DynamicImageFormat.Jpeg)
		], variants);
	}

	[Fact]
	public void MultipleComponentsInSameBuilderBlockEmitsVariantsForAll()
	{
		const string source = """
using Umbrella.DynamicImage.Abstractions;

public static class RenderFragmentFactory
{
	public static void Build(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
	{
		builder.OpenComponent<Umbrella.AspNetCore.Blazor.Components.DynamicImage.UmbrellaDynamicImage>(0);
		builder.AddAttribute(1, "Url", "/images/first.jpg");
		builder.AddAttribute(2, "WidthRequest", 300);
		builder.AddAttribute(3, "HeightRequest", 200);
		builder.AddAttribute(4, "MaxPixelDensity", 1);
		builder.AddAttribute(5, "ImageFormat", DynamicImageFormat.Png);
		builder.CloseComponent();

		builder.OpenComponent<Umbrella.AspNetCore.Blazor.Components.DynamicImage.UmbrellaDynamicImage>(6);
		builder.AddAttribute(7, "Url", "/images/second.jpg");
		builder.AddAttribute(8, "WidthRequest", 600);
		builder.AddAttribute(9, "HeightRequest", 400);
		builder.AddAttribute(10, "MaxPixelDensity", 1);
		builder.AddAttribute(11, "ImageFormat", DynamicImageFormat.WebP);
		builder.CloseComponent();
	}
}
""" + SharedComponentInfrastructureSource;

		DynamicImageVariant[] variants = GenerateVariants(source);

		Assert.Equal(
		[
			new DynamicImageVariant(300, 200, DynamicResizeMode.Crop, DynamicImageFormat.Png),
			new DynamicImageVariant(600, 400, DynamicResizeMode.Crop, DynamicImageFormat.WebP)
		], variants);
	}

	[Fact]
	public void NoAttributesUsesAllDefaultsAndEmitsDensityVariants()
	{
		const string source = """
public static class RenderFragmentFactory
{
	public static void Build(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
	{
		builder.OpenComponent<Umbrella.AspNetCore.Blazor.Components.DynamicImage.UmbrellaDynamicImage>(0);
		builder.CloseComponent();
	}
}
""" + SharedComponentInfrastructureSource;

		DynamicImageVariant[] variants = GenerateVariants(source);

		// Defaults: W=1, H=1, Crop, Jpeg, MaxPixelDensity=3
		Assert.Equal(
		[
			new DynamicImageVariant(1, 1, DynamicResizeMode.Crop, DynamicImageFormat.Jpeg),
			new DynamicImageVariant(2, 2, DynamicResizeMode.Crop, DynamicImageFormat.Jpeg),
			new DynamicImageVariant(3, 3, DynamicResizeMode.Crop, DynamicImageFormat.Jpeg)
		], variants);
	}

	[Fact]
	public void DuplicateVariantsAcrossComponentsAreDeduped()
	{
		const string source = """
using Umbrella.DynamicImage.Abstractions;

public static class RenderFragmentFactory
{
	public static void Build(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
	{
		builder.OpenComponent<Umbrella.AspNetCore.Blazor.Components.DynamicImage.UmbrellaDynamicImage>(0);
		builder.AddAttribute(1, "Url", "/images/first.jpg");
		builder.AddAttribute(2, "WidthRequest", 100);
		builder.AddAttribute(3, "HeightRequest", 50);
		builder.AddAttribute(4, "MaxPixelDensity", 1);
		builder.CloseComponent();

		builder.OpenComponent<Umbrella.AspNetCore.Blazor.Components.DynamicImage.UmbrellaDynamicImage>(5);
		builder.AddAttribute(6, "Url", "/images/second.jpg");
		builder.AddAttribute(7, "WidthRequest", 100);
		builder.AddAttribute(8, "HeightRequest", 50);
		builder.AddAttribute(9, "MaxPixelDensity", 1);
		builder.CloseComponent();
	}
}
""" + SharedComponentInfrastructureSource;

		DynamicImageVariant[] variants = GenerateVariants(source);

		// Both components produce (100, 50, Crop, Jpeg) — should appear only once
		Assert.Equal(
		[
			new DynamicImageVariant(100, 50, DynamicResizeMode.Crop, DynamicImageFormat.Jpeg)
		], variants);
	}

	[Fact]
	public void StaticHttpUrlOnFirstComponentDoesNotSuppressSecondComponent()
	{
		const string source = """
using Umbrella.DynamicImage.Abstractions;

public static class RenderFragmentFactory
{
	public static void Build(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
	{
		builder.OpenComponent<Umbrella.AspNetCore.Blazor.Components.DynamicImage.UmbrellaDynamicImage>(0);
		builder.AddAttribute(1, "Url", "https://cdn.example.com/logo.png");
		builder.AddAttribute(2, "WidthRequest", 300);
		builder.AddAttribute(3, "HeightRequest", 200);
		builder.AddAttribute(4, "MaxPixelDensity", 1);
		builder.CloseComponent();

		builder.OpenComponent<Umbrella.AspNetCore.Blazor.Components.DynamicImage.UmbrellaDynamicImage>(5);
		builder.AddAttribute(6, "Url", "/images/local.jpg");
		builder.AddAttribute(7, "WidthRequest", 400);
		builder.AddAttribute(8, "HeightRequest", 300);
		builder.AddAttribute(9, "MaxPixelDensity", 1);
		builder.CloseComponent();
	}
}
""" + SharedComponentInfrastructureSource;

		DynamicImageVariant[] variants = GenerateVariants(source);

		// First component (HTTPS URL) → no variants; second component → emits one variant
		Assert.Equal(
		[
			new DynamicImageVariant(400, 300, DynamicResizeMode.Crop, DynamicImageFormat.Jpeg)
		], variants);
	}

	// ─── Tag-helper discovery tests ───────────────────────────────────────────

	[Fact]
	public void TagHelperBasicPropertiesEmitPixelDensityVariants()
	{
		const string source = """
using Umbrella.DynamicImage.Abstractions;
using Umbrella.AspNetCore.WebUtilities.DynamicImage.Mvc.TagHelpers;

public class MyView : RazorPageBase
{
	private DynamicImageTagHelper __DynamicImageTagHelper = default!;

	public void Execute()
	{
		__DynamicImageTagHelper = CreateTagHelper<DynamicImageTagHelper>();
		__DynamicImageTagHelper.WidthRequest = 200;
		__DynamicImageTagHelper.HeightRequest = 100;
		__DynamicImageTagHelper.ResizeMode = DynamicResizeMode.CropFocalPoint;
		__DynamicImageTagHelper.ImageFormat = DynamicImageFormat.WebP;
	}
}
""" + SharedTagHelperInfrastructureSource;

		DynamicImageVariant[] variants = GenerateVariants(source);

		Assert.Equal(
		[
			new DynamicImageVariant(200, 100, DynamicResizeMode.CropFocalPoint, DynamicImageFormat.WebP)
			,
			new DynamicImageVariant(400, 200, DynamicResizeMode.CropFocalPoint, DynamicImageFormat.WebP),
			new DynamicImageVariant(600, 300, DynamicResizeMode.CropFocalPoint, DynamicImageFormat.WebP)
		], variants);
	}

	[Fact]
	public void TagHelperWithSizeWidthsEmitsBaseAndResponsiveVariants()
	{
		const string source = """
using Umbrella.DynamicImage.Abstractions;
using Umbrella.AspNetCore.WebUtilities.DynamicImage.Mvc.TagHelpers;

public class MyView : RazorPageBase
{
	private DynamicImageTagHelper __DynamicImageTagHelper = default!;

	public void Execute()
	{
		__DynamicImageTagHelper = CreateTagHelper<DynamicImageTagHelper>();
		__DynamicImageTagHelper.WidthRequest = 200;
		__DynamicImageTagHelper.HeightRequest = 100;
		__DynamicImageTagHelper.ImageFormat = DynamicImageFormat.Png;
		__DynamicImageTagHelper.SizeWidths = "100, 200";
	}
}
""" + SharedTagHelperInfrastructureSource;

		DynamicImageVariant[] variants = GenerateVariants(source);

		// base: (200,100), sizeWidth=100 × densities 1-3 → 100x50, 200x100, 300x150
		// sizeWidth=200 × densities 1-3 → 200x100, 400x200, 600x300; deduped
		Assert.Equal(
		[
			new DynamicImageVariant(100, 50, DynamicResizeMode.Crop, DynamicImageFormat.Png),
			new DynamicImageVariant(200, 100, DynamicResizeMode.Crop, DynamicImageFormat.Png),
			new DynamicImageVariant(300, 150, DynamicResizeMode.Crop, DynamicImageFormat.Png),
			new DynamicImageVariant(400, 200, DynamicResizeMode.Crop, DynamicImageFormat.Png),
			new DynamicImageVariant(600, 300, DynamicResizeMode.Crop, DynamicImageFormat.Png)
		], variants);
	}

	[Fact]
	public void TagHelperCustomPixelDensityLimitsExpansion()
	{
		const string source = """
using Umbrella.AspNetCore.WebUtilities.DynamicImage.Mvc.TagHelpers;

public class MyView : RazorPageBase
{
	private DynamicImageTagHelper __DynamicImageTagHelper = default!;

	public void Execute()
	{
		__DynamicImageTagHelper = CreateTagHelper<DynamicImageTagHelper>();
		__DynamicImageTagHelper.WidthRequest = 320;
		__DynamicImageTagHelper.HeightRequest = 240;
		__DynamicImageTagHelper.ImageMaxPixelDensity = 2;
	}
}
""" + SharedTagHelperInfrastructureSource;

		DynamicImageVariant[] variants = GenerateVariants(source);

		Assert.Equal(
		[
			new DynamicImageVariant(320, 240, DynamicResizeMode.Crop, DynamicImageFormat.Jpeg),
			new DynamicImageVariant(640, 480, DynamicResizeMode.Crop, DynamicImageFormat.Jpeg)
		], variants);
	}

	[Fact]
	public void TagHelperDefaultResizeModeAndFormatApplied()
	{
		const string source = """
using Umbrella.AspNetCore.WebUtilities.DynamicImage.Mvc.TagHelpers;

public class MyView : RazorPageBase
{
	private DynamicImageTagHelper __DynamicImageTagHelper = default!;

	public void Execute()
	{
		__DynamicImageTagHelper = CreateTagHelper<DynamicImageTagHelper>();
		__DynamicImageTagHelper.WidthRequest = 320;
		__DynamicImageTagHelper.HeightRequest = 240;
	}
}
""" + SharedTagHelperInfrastructureSource;

		DynamicImageVariant[] variants = GenerateVariants(source);

		// Defaults: ResizeMode=Crop (4), ImageFormat=Jpeg (2)
		Assert.Equal(
		[
			new DynamicImageVariant(320, 240, DynamicResizeMode.Crop, DynamicImageFormat.Jpeg),
			new DynamicImageVariant(640, 480, DynamicResizeMode.Crop, DynamicImageFormat.Jpeg),
			new DynamicImageVariant(960, 720, DynamicResizeMode.Crop, DynamicImageFormat.Jpeg)
		], variants);
	}

	[Fact]
	public void TagHelperDynamicWidthAndHeightSkipsVariant()
	{
		const string source = """
using Umbrella.AspNetCore.WebUtilities.DynamicImage.Mvc.TagHelpers;

public class MyView : RazorPageBase
{
	private DynamicImageTagHelper __DynamicImageTagHelper = default!;

	public void Execute(int w, int h)
	{
		__DynamicImageTagHelper = CreateTagHelper<DynamicImageTagHelper>();
		__DynamicImageTagHelper.WidthRequest = w;
		__DynamicImageTagHelper.HeightRequest = h;
	}
}
""" + SharedTagHelperInfrastructureSource;

		DynamicImageVariant[] variants = GenerateVariants(source);

		// Both dimensions unknown (0) → no variant emitted
		Assert.Empty(variants);
	}

	[Fact]
	public void TagHelperPictureSourceEmitsPixelDensityVariantsWithoutSizeWidths()
	{
		const string source = """
using Umbrella.DynamicImage.Abstractions;
using Umbrella.AspNetCore.WebUtilities.DynamicImage.Mvc.TagHelpers;

public class MyView : RazorPageBase
{
	private DynamicImagePictureSourceTagHelper __DynamicImagePictureSourceTagHelper = default!;

	public void Execute()
	{
		__DynamicImagePictureSourceTagHelper = CreateTagHelper<DynamicImagePictureSourceTagHelper>();
		__DynamicImagePictureSourceTagHelper.WidthRequest = 800;
		__DynamicImagePictureSourceTagHelper.HeightRequest = 400;
		__DynamicImagePictureSourceTagHelper.ImageFormat = DynamicImageFormat.WebP;
	}
}
""" + SharedTagHelperInfrastructureSource;

		DynamicImageVariant[] variants = GenerateVariants(source);

		Assert.Equal(
		[
			new DynamicImageVariant(800, 400, DynamicResizeMode.Crop, DynamicImageFormat.WebP),
			new DynamicImageVariant(1600, 800, DynamicResizeMode.Crop, DynamicImageFormat.WebP),
			new DynamicImageVariant(2400, 1200, DynamicResizeMode.Crop, DynamicImageFormat.WebP)
		], variants);
	}

	[Fact]
	public void TagHelperPictureSourceIgnoresSizeWidthsProperty()
	{
		// DynamicImagePictureSourceTagHelper has no SizeWidths; assigning it in
		// generated code should not expand variants.
		const string source = """
using Umbrella.AspNetCore.WebUtilities.DynamicImage.Mvc.TagHelpers;

public class MyView : RazorPageBase
{
	private DynamicImagePictureSourceTagHelper __DynamicImagePictureSourceTagHelper = default!;

	public void Execute()
	{
		__DynamicImagePictureSourceTagHelper = CreateTagHelper<DynamicImagePictureSourceTagHelper>();
		__DynamicImagePictureSourceTagHelper.WidthRequest = 640;
		__DynamicImagePictureSourceTagHelper.HeightRequest = 480;
	}
}
""" + SharedTagHelperInfrastructureSource;

		DynamicImageVariant[] variants = GenerateVariants(source);

		Assert.Equal(
		[
			new DynamicImageVariant(640, 480, DynamicResizeMode.Crop, DynamicImageFormat.Jpeg),
			new DynamicImageVariant(1280, 960, DynamicResizeMode.Crop, DynamicImageFormat.Jpeg),
			new DynamicImageVariant(1920, 1440, DynamicResizeMode.Crop, DynamicImageFormat.Jpeg)
		], variants);
	}

	[Fact]
	public void TagHelperMultipleInstancesInSameMethodEmitsVariantsForAll()
	{
		const string source = """
using Umbrella.DynamicImage.Abstractions;
using Umbrella.AspNetCore.WebUtilities.DynamicImage.Mvc.TagHelpers;

public class MyView : RazorPageBase
{
	private DynamicImageTagHelper __DynamicImageTagHelper = default!;

	public void Execute()
	{
		__DynamicImageTagHelper = CreateTagHelper<DynamicImageTagHelper>();
		__DynamicImageTagHelper.WidthRequest = 300;
		__DynamicImageTagHelper.HeightRequest = 200;
		__DynamicImageTagHelper.ImageFormat = DynamicImageFormat.Png;

		__DynamicImageTagHelper = CreateTagHelper<DynamicImageTagHelper>();
		__DynamicImageTagHelper.WidthRequest = 600;
		__DynamicImageTagHelper.HeightRequest = 400;
		__DynamicImageTagHelper.ImageFormat = DynamicImageFormat.WebP;
	}
}
""" + SharedTagHelperInfrastructureSource;

		DynamicImageVariant[] variants = GenerateVariants(source);

		Assert.Equal(
		[
			new DynamicImageVariant(300, 200, DynamicResizeMode.Crop, DynamicImageFormat.Png),
			new DynamicImageVariant(600, 400, DynamicResizeMode.Crop, DynamicImageFormat.Png),
			new DynamicImageVariant(600, 400, DynamicResizeMode.Crop, DynamicImageFormat.WebP),
			new DynamicImageVariant(900, 600, DynamicResizeMode.Crop, DynamicImageFormat.Png),
			new DynamicImageVariant(1200, 800, DynamicResizeMode.Crop, DynamicImageFormat.WebP),
			new DynamicImageVariant(1800, 1200, DynamicResizeMode.Crop, DynamicImageFormat.WebP)
		], variants);
	}

	[Fact]
	public void TagHelperDuplicateVariantsAreDeduped()
	{
		const string source = """
using Umbrella.AspNetCore.WebUtilities.DynamicImage.Mvc.TagHelpers;

public class MyView : RazorPageBase
{
	private DynamicImageTagHelper __DynamicImageTagHelper = default!;

	public void Execute()
	{
		__DynamicImageTagHelper = CreateTagHelper<DynamicImageTagHelper>();
		__DynamicImageTagHelper.WidthRequest = 100;
		__DynamicImageTagHelper.HeightRequest = 50;

		__DynamicImageTagHelper = CreateTagHelper<DynamicImageTagHelper>();
		__DynamicImageTagHelper.WidthRequest = 100;
		__DynamicImageTagHelper.HeightRequest = 50;
	}
}
""" + SharedTagHelperInfrastructureSource;

		DynamicImageVariant[] variants = GenerateVariants(source);

		Assert.Equal(
		[
			new DynamicImageVariant(100, 50, DynamicResizeMode.Crop, DynamicImageFormat.Jpeg),
			new DynamicImageVariant(200, 100, DynamicResizeMode.Crop, DynamicImageFormat.Jpeg),
			new DynamicImageVariant(300, 150, DynamicResizeMode.Crop, DynamicImageFormat.Jpeg)
		], variants);
	}

	[Fact]
	public void TagHelperAndComponentVariantsAreUnifiedInCatalog()
	{
		const string source = """
using Umbrella.DynamicImage.Abstractions;
using Umbrella.AspNetCore.WebUtilities.DynamicImage.Mvc.TagHelpers;

public class MyView : RazorPageBase
{
	private DynamicImageTagHelper __DynamicImageTagHelper = default!;

	public void Execute()
	{
		__DynamicImageTagHelper = CreateTagHelper<DynamicImageTagHelper>();
		__DynamicImageTagHelper.WidthRequest = 400;
		__DynamicImageTagHelper.HeightRequest = 300;
		__DynamicImageTagHelper.ImageFormat = DynamicImageFormat.Png;
	}
}

public static class RenderFragmentFactory
{
	public static void Build(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
	{
		builder.OpenComponent<Umbrella.AspNetCore.Blazor.Components.DynamicImage.UmbrellaDynamicImage>(0);
		builder.AddAttribute(1, "Url", "/images/hero.jpg");
		builder.AddAttribute(2, "WidthRequest", 800);
		builder.AddAttribute(3, "HeightRequest", 600);
		builder.AddAttribute(4, "MaxPixelDensity", 1);
		builder.AddAttribute(5, "ImageFormat", DynamicImageFormat.WebP);
		builder.CloseComponent();
	}
}
""" + SharedTagHelperInfrastructureSource + SharedComponentInfrastructureSource;

		DynamicImageVariant[] variants = GenerateVariants(source);

		Assert.Equal(
		[
			new DynamicImageVariant(400, 300, DynamicResizeMode.Crop, DynamicImageFormat.Png),
			new DynamicImageVariant(800, 600, DynamicResizeMode.Crop, DynamicImageFormat.Png),
			new DynamicImageVariant(800, 600, DynamicResizeMode.Crop, DynamicImageFormat.WebP)
			,
			new DynamicImageVariant(1200, 900, DynamicResizeMode.Crop, DynamicImageFormat.Png)
		], variants);
	}

	[Fact]
	public void TagHelperSizeWidthsWithInvalidEntriesFiltersThemOut()
	{
		const string source = """
using Umbrella.AspNetCore.WebUtilities.DynamicImage.Mvc.TagHelpers;

public class MyView : RazorPageBase
{
	private DynamicImageTagHelper __DynamicImageTagHelper = default!;

	public void Execute()
	{
		__DynamicImageTagHelper = CreateTagHelper<DynamicImageTagHelper>();
		__DynamicImageTagHelper.WidthRequest = 200;
		__DynamicImageTagHelper.HeightRequest = 100;
		__DynamicImageTagHelper.SizeWidths = "0,-5,100";
	}
}
""" + SharedTagHelperInfrastructureSource;

		DynamicImageVariant[] variants = GenerateVariants(source);

		// Only sizeWidth=100 is valid; densities 1-3 → 100x50, 200x100, 300x150; plus base 200x100 (deduped)
		Assert.Equal(
		[
			new DynamicImageVariant(100, 50, DynamicResizeMode.Crop, DynamicImageFormat.Jpeg),
			new DynamicImageVariant(200, 100, DynamicResizeMode.Crop, DynamicImageFormat.Jpeg),
			new DynamicImageVariant(300, 150, DynamicResizeMode.Crop, DynamicImageFormat.Jpeg)
		], variants);
	}

	private const string SharedTagHelperInfrastructureSource = """

namespace Umbrella.AspNetCore.WebUtilities.DynamicImage.Mvc.TagHelpers
{
	public class DynamicImageTagHelper
	{
		public int WidthRequest { get; set; }
		public int HeightRequest { get; set; }
		public int ImageMaxPixelDensity { get; set; } = 3;
		public Umbrella.DynamicImage.Abstractions.DynamicResizeMode ResizeMode { get; set; }
		public Umbrella.DynamicImage.Abstractions.DynamicImageFormat ImageFormat { get; set; }
		public string? SizeWidths { get; set; }
	}

	public class DynamicImagePictureSourceTagHelper
	{
		public int WidthRequest { get; set; }
		public int HeightRequest { get; set; }
		public int ImageMaxPixelDensity { get; set; } = 3;
		public Umbrella.DynamicImage.Abstractions.DynamicResizeMode ResizeMode { get; set; }
		public Umbrella.DynamicImage.Abstractions.DynamicImageFormat ImageFormat { get; set; }
	}
}

public abstract class RazorPageBase
{
	protected T CreateTagHelper<T>() where T : new() => new T();
}
""";

	private static DynamicImageVariant[] GenerateVariants(string source)
	{
		CSharpCompilation compilation = CreateCompilation(source);
		var generator = new DynamicImageComponentVariantSourceGenerator();
		GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

		driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation outputCompilation, out ImmutableArray<Diagnostic> diagnostics);

		Assert.Empty(diagnostics);

		GeneratorDriverRunResult runResult = driver.GetRunResult();
		Assert.Empty(runResult.Diagnostics);

		Assembly assembly = EmitAssembly(outputCompilation);
		Type catalogType = assembly.GetType("Umbrella.Generated.DynamicImage.UmbrellaDynamicImageComponentVariantCatalog", throwOnError: true)!;

		return ((IEnumerable<DynamicImageVariant>)catalogType.GetField("All", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!)
			.OrderBy(x => x.Width)
			.ThenBy(x => x.Height)
			.ThenBy(x => (int)x.ResizeMode)
			.ThenBy(x => (int)x.Format)
			.ToArray();
	}

	private static CSharpCompilation CreateCompilation(string source)
	{
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
			assemblyName: "Umbrella.Generators.DynamicImage.Test.Consumer",
			syntaxTrees: [syntaxTree],
			references: references,
			options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
	}

	private static Assembly EmitAssembly(Compilation compilation)
	{
		using var stream = new MemoryStream();
		EmitResult emitResult = compilation.Emit(stream);

		if (!emitResult.Success)
		{
			string diagnostics = string.Join(
				Environment.NewLine,
				emitResult.Diagnostics
					.Where(x => x.Severity is DiagnosticSeverity.Error)
					.Select(x => x.ToString()));

			throw new Xunit.Sdk.XunitException($"Compilation failed:{Environment.NewLine}{diagnostics}");
		}

		return Assembly.Load(stream.ToArray());
	}

	private const string SharedComponentInfrastructureSource = """

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
""";
}
