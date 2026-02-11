
using Microsoft.Extensions.DependencyInjection;
using Umbrella.Maui.Exceptions;

namespace Umbrella.Maui;

/// <summary>
/// A static class used to provide the <see cref="Maui"/> package to registered application services for use internally.
/// </summary>
public static class UmbrellaMauiServices
{
	private static IServiceProvider? _services;

	/// <summary>
	/// Gets or sets the services.
	/// </summary>
	/// <exception cref="UmbrellaMauiException">The IServiceProvider has not been assigned. This should be done in Startup.cs.</exception>
	/// <exception cref="ArgumentNullException">Services</exception>
	public static IServiceProvider Services
	{
		get => _services ?? throw new UmbrellaMauiException("The IServiceProvider has not been assigned. This should be done in Startup.cs.");
		set => _services = value ?? throw new ArgumentNullException(nameof(Services));
	}

	/// <summary>
	/// Gets the service of type <typeparamref name="T"/>.
	/// </summary>
	/// <typeparam name="T">The type of the service to get.</typeparam>
	/// <returns>The service of the specified type <typeparamref name="T"/>.</returns>
	public static T GetService<T>() where T : notnull => Services.GetRequiredService<T>();
}