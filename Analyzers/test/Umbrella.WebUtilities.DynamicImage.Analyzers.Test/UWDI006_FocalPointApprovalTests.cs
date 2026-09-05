namespace Umbrella.WebUtilities.DynamicImage.Analyzers.Test;

public class UWDI006_FocalPointApprovalTests : AnalyzerTestBase<Umbrella.WebUtilities.DynamicImage.Analyzers.DynamicImageVersioningAnalyzer>
{
	private const string Infrastructure = """
namespace Umbrella.AspNetCore.Blazor.Components.DynamicImage { public class UmbrellaDynamicImage { } }
namespace Umbrella.DynamicImage.Abstractions { public class DynamicImageDescriptor { } }
""";

	[Fact]
	public async Task SeparateFocalCoordinatesWithoutApprovalWarn()
	{
		const string razor = """
@using Umbrella.AspNetCore.Blazor.Components.DynamicImage
<UmbrellaDynamicImage WidthRequest="100" HeightRequest="50" FocalPointX="@Model.X" FocalPointY="@Model.Y" />
""";
		await VerifyAnalyzerWithAdditionalFilesAsync(Infrastructure, [("C:/app/Test.razor", razor)],
			Diagnostic(Umbrella.WebUtilities.DynamicImage.Analyzers.DynamicImageVersioningAnalyzer.MissingFocalApprovalRule, 2, 61));
	}

	[Theory]
	[InlineData("Image=\"@Model.Image\"")]
	[InlineData("FocalPointX=\"@Model.X\" FocalPointY=\"@Model.Y\" FocalPointApproval=\"@Model.Approval\"")]
	public async Task DescriptorOrApprovalDoesNotWarn(string attributes)
	{
		string razor = "@using Umbrella.AspNetCore.Blazor.Components.DynamicImage\n<UmbrellaDynamicImage WidthRequest=\"100\" HeightRequest=\"50\" " + attributes + " />";
		await VerifyAnalyzerWithAdditionalFilesAsync(Infrastructure, [("C:/app/Test.razor", razor)]);
	}
}
