
using CommunityToolkit.Diagnostics;
using Umbrella.AppFramework.Services.Abstractions;
using Umbrella.Utilities.Networking.Abstractions;
using Umbrella.Xamarin.Networking;
using Umbrella.Xamarin.ObjectModel;
using Umbrella.Xamarin.ObjectModel.Abstractions;
using Umbrella.Xamarin.Utilities;
using Umbrella.Xamarin.Utilities.Abstractions;
using Umbrella.Xamarin.Utilities.Options;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods used to register services for the <see cref="Umbrella.Xamarin"/> package with a specified
/// <see cref="IServiceCollection"/> dependency injection container builder.
/// </summary>
public static class IServiceCollectionExtensions
{
	/// <summary>
	/// Adds the <see cref="Umbrella.Xamarin"/> services to the specified <see cref="IServiceCollection"/> dependency injection container builder.
	/// </summary>
	/// <param name="services">The services.</param>
	/// <param name="permissionsUtilityOptionsBuilder">The optional <see cref="PermissionsUtilityOptions"/> builder.</param>
	/// <returns>The services builder.</returns>
	public static IServiceCollection AddUmbrellaXamarin(
		this IServiceCollection services,
		Action<IServiceProvider, PermissionsUtilityOptions>? permissionsUtilityOptionsBuilder = null)
	{
		Guard.IsNotNull(services, nameof(services));

		_ = services.AddSingleton<IAppLocalStorageService, XamarinLocalStorageService>();
		_ = services.AddSingleton(x => (IAppSessionStorageService)x.GetRequiredService<IAppLocalStorageService>());
		_ = services.AddSingleton<IDialogService, DialogUtility>();
		_ = services.AddSingleton<INetworkConnectionStatusUtility, NetworkConnectionStatusUtility>();
		_ = services.AddSingleton<IPermissionsUtility, PermissionsUtility>();
		_ = services.AddSingleton<IUmbrellaCommandFactory, UmbrellaCommandFactory>();
		_ = services.AddSingleton<IUriNavigatorService, UriNavigator>();
		_ = services.AddSingleton<IXamarinValidationUtility, XamarinValidationUtility>();

		_ = services.ConfigureUmbrellaOptions(permissionsUtilityOptionsBuilder);

		return services;
	}
}