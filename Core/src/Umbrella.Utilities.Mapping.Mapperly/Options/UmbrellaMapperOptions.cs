
using System.Reflection;
using Umbrella.Utilities.Mapping.Mapperly.Abstractions;
using Umbrella.Utilities.Mapping.Mapperly.Enumerations;
using Umbrella.Utilities.Options.Abstractions;

namespace Umbrella.Utilities.Mapping.Mapperly.Options;

/// <summary>
/// Options for use with the <see cref="UmbrellaMapper"/>.
/// </summary>
/// <seealso cref="ISanitizableUmbrellaOptions" />
/// <seealso cref="IValidatableUmbrellaOptions" />
public class UmbrellaMapperOptions : ISanitizableUmbrellaOptions, IValidatableUmbrellaOptions
{
	/// <summary>
	/// Gets or sets the target assemblies to scan for types that implement <see cref="IUmbrellaMapperlyMapper{TSource, TDestination}"/>.
	/// </summary>
	/// <remarks>
	/// This should be specified only if <see cref="TargetAssemblyNamePrefix"/> is not set.
	/// </remarks>
	public IReadOnlyCollection<Assembly> TargetAssemblies { get; set; } = Array.Empty<Assembly>();

	/// <summary>
	/// Gets or sets the prefix of assemblies to scan for types that implement <see cref="IUmbrellaMapperlyMapper{TSource, TDestination}"/>.
	/// </summary>
	/// <remarks>
	/// This should be specified only if <see cref="TargetAssemblies"/> is not set.
	/// </remarks>
	public string? TargetAssemblyNamePrefix { get; set; }

	/// <summary>
	/// Gets or sets the environment.
	/// </summary>
	/// <remarks>Defaults to <see cref="MapperlyEnvironmentType.Server"/>.</remarks>
	public MapperlyEnvironmentType Environment { get; set; } = MapperlyEnvironmentType.Server;

	/// <inheritdoc/>
	public void Sanitize()
	{
		TargetAssemblies = TargetAssemblies?.Distinct().ToArray()!;
		TargetAssemblyNamePrefix = TargetAssemblyNamePrefix?.Trim();
	}

	/// <inheritdoc/>
	public void Validate()
	{
		if (TargetAssemblies is not { Count: > 0 } && string.IsNullOrWhiteSpace(TargetAssemblyNamePrefix))
			throw new ArgumentException("Either TargetAssemblies or TargetAssemblyNamePrefix must be set.", nameof(TargetAssemblies));

		if (TargetAssemblies is { Count: > 0 } && !string.IsNullOrWhiteSpace(TargetAssemblyNamePrefix))
			throw new ArgumentException("Only one of TargetAssemblies or TargetAssemblyNamePrefix can be set.", nameof(TargetAssemblies));
	}
}