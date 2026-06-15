
using CommunityToolkit.Diagnostics;
using Umbrella.AppFramework.Services.Abstractions;
using Umbrella.AspNetCore.Blazor.Components.Breadcrumb.Options;
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
	/// <remarks>
	/// <para>
	/// The following services require an <b>interactive render mode</b> (Interactive Server or WebAssembly).
	/// They will gracefully no-op during static rendering / prerendering but become fully functional once
	/// the interactive circuit or WASM runtime is active:
	/// <list type="bullet">
	///   <item><description><see cref="IAppLocalStorageService"/> (<see cref="BlazorLocalStorageService"/>)</description></item>
	///   <item><description><see cref="IAppSessionStorageService"/> (<see cref="BlazorSessionStorageService"/>)</description></item>
	///   <item><description><see cref="IUmbrellaBlazorInteropService"/> (<see cref="UmbrellaBlazorInteropService"/>)</description></item>
	///   <item><description><see cref="IBrowserEventAggregator"/> (<see cref="BrowserEventAggregator"/>)</description></item>
	/// </list>
	/// </para>
	/// <para>
	/// <see cref="IUriNavigatorService"/> (<see cref="UriNavigatorService"/>) supports same-window navigation in all render modes.
	/// Opening a URI in a new window (<c>openInNewWindow = true</c>) requires interactive rendering.
	/// </para>
	/// <para>
	/// <see cref="IHttpContextService"/> is registered as a no-op (<see cref="NoopHttpContextService"/>) because
	/// there is no HTTP context available in Blazor WebAssembly or in the interactive Blazor Server circuit.
	/// Applications that need the HTTP context during static rendering should resolve it directly from
	/// <c>IHttpContextAccessor</c> in a non-Blazor layer.
	/// </para>
	/// </remarks>
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

		return services;
	}
}