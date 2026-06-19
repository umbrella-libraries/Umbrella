using Umbrella.AspNetCore.Blazor.Components.Breadcrumb.Options;

namespace Umbrella.AspNetCore.Blazor.Components.Breadcrumb;

/// <summary>
/// A non-rendering component that represents a breadcrumb navigation structure that will be rendered by the <see cref="UmbrellaBreadcrumbRenderer" /> component.
/// This component should have one or more <see cref="UmbrellaBreadcrumbItem"/> child components.
/// </summary>
/// <seealso cref="ComponentBase" />
public partial class UmbrellaBreadcrumb
{
	internal const string SectionOutletSectionName = "Umbrella.AspNetCore.Blazor.Components.Breadcrumb.UmbrellaBreadcrumb";

	[Inject]
	private UmbrellaBreadcrumbOptions Options { get; set; } = null!;

	[Inject]
	private IServiceProvider ServiceProvider { get; set; } = null!;

	/// <summary>
	/// Gets or sets the child content of this component. This should be a collection of <see cref="UmbrellaBreadcrumbItem"/> components.
	/// </summary>
	[Parameter]
	public RenderFragment ChildContent { get; set; } = null!;

	internal List<UmbrellaBreadcrumbItem> BreadcrumbItemList { get; } = [];

	private string EffectiveRootName => Options.RootNameFactory?.Invoke(ServiceProvider) ?? Options.RootName;
	private string EffectiveRootPath => Options.RootPathFactory?.Invoke(ServiceProvider) ?? Options.RootPath;
}
