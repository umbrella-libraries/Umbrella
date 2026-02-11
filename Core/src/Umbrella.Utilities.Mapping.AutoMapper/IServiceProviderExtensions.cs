
using AutoMapper;
using Umbrella.Utilities.Exceptions;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// AutoMapper specific extension methods that operate on the <see cref="IServiceProvider"/> type.
/// </summary>
public static class IServiceProviderExtensions
{
	/// <summary>
	/// Asserts the automatic mapper configuration is valid.
	/// </summary>
	/// <param name="serviceProvider">The service provider.</param>
	/// <remarks>
	/// Internally, this calls into <see cref="T:IConfigurationProvider.AssertConfigurationIsValid"/>
	/// which throws an <see cref="AutoMapperConfigurationException"/> for each problem.
	/// </remarks>
	/// <exception cref="AutoMapperConfigurationException">Thrown for each AutoMapper configuration problem.</exception>
	/// <exception cref="UmbrellaException">Thrown if an <see cref="IMapper"/> instance is not registered with the service provider.</exception>
	public static void AssertAutoMapperConfigurationIsValid(this IServiceProvider serviceProvider)
	{
		var mapper = serviceProvider.GetRequiredService<IMapper>();

		if (mapper is null)
			throw new UmbrellaException("The mapper instance is not registered with the service provider.");

		mapper.ConfigurationProvider.AssertConfigurationIsValid();
	}
}