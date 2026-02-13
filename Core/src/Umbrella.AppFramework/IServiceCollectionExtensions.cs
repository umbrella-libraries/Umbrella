
using CommunityToolkit.Diagnostics;
using Umbrella.AppFramework.Http.Handlers;
using Umbrella.AppFramework.Http.Handlers.Options;
using Umbrella.AppFramework.Options;
using Umbrella.AppFramework.Security;
using Umbrella.AppFramework.Security.Abstractions;
using Umbrella.AppFramework.Security.Options;
using Umbrella.AppFramework.Services;
using Umbrella.AppFramework.Services.Abstractions;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods used to register services for the <see cref="Umbrella.AppFramework"/> package with a specified
/// <see cref="IServiceCollection"/> dependency injection container builder.
/// </summary>
public static class IServiceCollectionExtensions
{
	/// <summary>
	/// Adds the <see cref="Umbrella.AppFramework"/> services to the specified <see cref="IServiceCollection"/> dependency
	/// injection container builder.
	/// </summary>
	/// <param name="services">The services.</param>
	/// <param name="appAuthHelperOptionsBuilder">The app auth helper options builder.</param>
	/// <param name="appAuthTokenStorageServiceOptionsBuilder">The app auth token storage service options builder.</param>
	/// <param name="dialogServiceOptionsBuilder">The dialog service options builder.</param>
	/// <returns>The services.</returns>
	public static IServiceCollection AddUmbrellaAppFramework(
		this IServiceCollection services,
		Action<IServiceProvider, AppAuthHelperOptions>? appAuthHelperOptionsBuilder = null,
		Action<IServiceProvider, AppAuthTokenStorageServiceOptions>? appAuthTokenStorageServiceOptionsBuilder = null,
		Action<IServiceProvider, DialogServiceOptions>? dialogServiceOptionsBuilder = null)
	{
		Guard.IsNotNull(services);

		// NB: These needs to be scoped for use with Blazor.
		_ = services.AddScoped<IAppAuthHelper, JwtAppAuthHelper>();
		_ = services.AddScoped<IAppUpdateMessageService, AppUpdateMessageService>();
		_ = services.AddScoped<IAppAuthTokenStorageService, AppAuthTokenStorageService>();

		_ = services.AddSingleton<IDialogTrackerService, DialogTrackerService>();
		_ = services.AddSingleton<ILoadingScreenService, LoadingScreenService>();

		// HTTP Delegating Handlers
		_ = services.AddScoped<AuthTokenHandler>();
		_ = services.AddScoped<RefreshedAuthTokenHandler>();
		_ = services.AddScoped<RequestNotificationHandler>();

		// Options
		_ = services.ConfigureUmbrellaOptions(appAuthHelperOptionsBuilder);
		_ = services.ConfigureUmbrellaOptions(appAuthTokenStorageServiceOptionsBuilder);
		_ = services.ConfigureUmbrellaOptions(dialogServiceOptionsBuilder);

		// Default call to configure the RequestNotificationHandlerOptions
		_ = services.ConfigureUmbrellaAppFramework();

		return services;
	}

	/// <summary>
	/// Configures the <see cref="Umbrella.AppFramework"/> services.
	/// </summary>
	/// <param name="services">The services.</param>
	/// <param name="requestNotificationHandlerOptionsBuilder">The request notification handler options builder.</param>
	/// <returns>The services.</returns>
	public static IServiceCollection ConfigureUmbrellaAppFramework(
		this IServiceCollection services,
		Action<IServiceProvider, RequestNotificationHandlerOptions>? requestNotificationHandlerOptionsBuilder = null)
	{
		// Options
		_ = services.ConfigureUmbrellaOptions(requestNotificationHandlerOptionsBuilder);

		return services;
	}
}