


using CommunityToolkit.Diagnostics;
using Umbrella.AppFramework.Services.Abstractions;
using Umbrella.Maui.Networking;
using Umbrella.Maui.ObjectModel;
using Umbrella.Maui.ObjectModel.Abstractions;
using Umbrella.Maui.Utilities;
using Umbrella.Maui.Utilities.Abstractions;
using Umbrella.Maui.Utilities.Options;
using Umbrella.Utilities.Networking.Abstractions;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods used to register services for the <see cref="Umbrella.Maui"/> package with a specified
/// <see cref="IServiceCollection"/> dependency injection container builder.
/// </summary>
public static class IServiceCollectionExtensions
{
	/// <summary>
	/// Adds the <see cref="Umbrella.Maui"/> services to the specified <see cref="IServiceCollection"/> dependency injection container builder.
	/// </summary>
	/// <param name="services">The services.</param>
	/// <param name="permissionsUtilityOptionsBuilder">The optional <see cref="PermissionsUtilityOptions"/> builder.</param>
	/// <returns>The services builder.</returns>
	public static IServiceCollection AddUmbrellaMaui(
		this IServiceCollection services,
		Action<IServiceProvider, PermissionsUtilityOptions>? permissionsUtilityOptionsBuilder = null)
	{
		Guard.IsNotNull(services, nameof(services));

		_ = services.AddSingleton<IAppLocalStorageService, MauiLocalStorageService>();
		_ = services.AddSingleton(x => (IAppSessionStorageService)x.GetRequiredService<IAppLocalStorageService>());
		_ = services.AddSingleton<IDialogService, DialogUtility>();
		_ = services.AddSingleton<INetworkConnectionStatusUtility, NetworkConnectionStatusUtility>();
		_ = services.AddSingleton<IPermissionsUtility, PermissionsUtility>();
		_ = services.AddSingleton<IUmbrellaCommandFactory, UmbrellaCommandFactory>();
		_ = services.AddSingleton<IUriNavigatorService, UriNavigator>();
		_ = services.AddSingleton<IMauiValidationUtility, MauiValidationUtility>();

		_ = services.ConfigureUmbrellaOptions(permissionsUtilityOptionsBuilder);

		return services;
	}
}