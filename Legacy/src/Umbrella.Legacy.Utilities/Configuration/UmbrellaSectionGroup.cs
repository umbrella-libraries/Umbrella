using System.Configuration;

namespace Umbrella.Legacy.Utilities.Configuration;

/// <summary>
/// Represents a group of config sections specifically for Umbrella sections.
/// </summary>
/// <seealso cref="ConfigurationSectionGroup" />
public class UmbrellaSectionGroup : ConfigurationSectionGroup
{
	#region Public Methods		
	/// <summary>
	/// Gets the configuration section.
	/// </summary>
	/// <typeparam name="T">The type of the config section.</typeparam>
	/// <param name="name">The name.</param>
	/// <returns>The config section.</returns>
	public T? GetConfigurationSection<T>(string name) where T : class => Sections[name] as T;
	#endregion

	#region Public Static Methods		
	/// <summary>
	/// Gets the section group.
	/// </summary>
	/// <param name="config">The configuration.</param>
	/// <returns>The section group.</returns>
	/// <exception cref="ArgumentNullException">The parameter {nameof(config)} cannot be null</exception>
	public static UmbrellaSectionGroup? GetSectionGroup(System.Configuration.Configuration config)
	{
		if (config is null)
			throw new ArgumentNullException(nameof(config));

		return config.GetSectionGroup("umbrella") as UmbrellaSectionGroup;
	}
	#endregion
}
