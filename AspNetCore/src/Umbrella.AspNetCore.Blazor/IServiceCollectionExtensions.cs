
using Blazored.LocalStorage;
using Blazored.Modal;
using Blazored.SessionStorage;
using CommunityToolkit.Diagnostics;
using Umbrella.AppFramework.Services.Abstractions;
using Umbrella.AspNetCore.Blazor.Components.Breadcrumb.Options;
using Umbrella.AspNetCore.Blazor.Components.Dialog;
using Umbrella.AspNetCore.Blazor.Components.Dialog.Abstractions;
using Umbrella.AspNetCore.Blazor.Components.DynamicImage.Options;
using Umbrella.AspNetCore.Blazor.Components.Grid.Options;
using Umbrella.AspNetCore.Blazor.Services;
using Umbrella.AspNetCore.Blazor.Services.Abstractions;
using Umbrella.AspNetCore.Blazor.Services.Grid;
using Umbrella.AspNetCore.Blazor.Services.Grid.Abstractions;
using Umbrella.AspNetCore.Shared.Services.Abstractions;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods used to register services for the <see cref="Umbrella.AspNetCore.Blazor"/> package with a specified
/// <see cref="IServiceCollection"/> dependency injection container builder.
/// </summary>
public static class IServiceCollectionExtensions
{
	/// <summary>
	/// Adds the <see cref="Umbrella.AspNetCore.Blazor"/> services to the specified <see cref="IServiceCollection"/> dependency injection container builder.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="umbrellaGridOptionsBuilder">Optional action used to configure the <see cref="UmbrellaGridOptions"/>.</param>
	/// <param name="umbrellaDynamicImageOptionsBuilder">Optional action used to configure the <see cref="UmbrellaDynamicImageOptions"/>.</param>
	/// <param name="umbrellaBreadcrumbOptionsBuilder">Optional action used to configure the <see cref="UmbrellaBreadcrumbOptions"/>.</param>
	/// <returns>The services builder.</returns>
	public static IServiceCollection AddUmbrellaBlazor(
		this IServiceCollection services,
		Action<IServiceProvider, UmbrellaGridOptions>? umbrellaGridOptionsBuilder = null,
		Action<IServiceProvider, UmbrellaDynamicImageOptions>? umbrellaDynamicImageOptionsBuilder = null,
		Action<IServiceProvider, UmbrellaBreadcrumbOptions>? umbrellaBreadcrumbOptionsBuilder = null)
	{
		Guard.IsNotNull(services);

		_ = services.AddScoped<IAppLocalStorageService, BlazorLocalStorageService>();
		_ = services.AddScoped<IAppSessionStorageService, BlazorSessionStorageService>();
		_ = services.AddScoped<IUmbrellaDialogService, UmbrellaDialogService>();
		_ = services.AddScoped<IUriNavigatorService, UriNavigatorService>();
		_ = services.AddTransient<IDialogService>(x => x.GetRequiredService<IUmbrellaDialogService>());
		_ = services.AddScoped<IUmbrellaBlazorInteropService, UmbrellaBlazorInteropService>();
		_ = services.AddScoped<IUmbrellaGridComponentServiceFactory, UmbrellaGridComponentServiceFactory>();
		_ = services.AddTransient<IBrowserEventAggregator, BrowserEventAggregator>();
		_ = services.AddScoped<IHttpContextService, NoopHttpContextService>();

		_ = services.ConfigureUmbrellaOptions(umbrellaGridOptionsBuilder);
		_ = services.ConfigureUmbrellaOptions(umbrellaDynamicImageOptionsBuilder);
		_ = services.ConfigureUmbrellaOptions(umbrellaBreadcrumbOptionsBuilder);

		// Add the Blazored Services here too to avoid the user having to add them manually
		_ = services.AddBlazoredModal();
		_ = services.AddBlazoredLocalStorage();
		_ = services.AddBlazoredSessionStorage();

		return services;
	}
}