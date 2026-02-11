


using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Umbrella.Utilities.Configuration.Abstractions;
using Umbrella.Utilities.TypeConverters.Abstractions;

namespace Umbrella.Utilities.Configuration;

/// <summary>
/// An abstract base class encapsulating the core behaviour of an application settings file.
/// </summary>
/// <seealso cref="ReadOnlyAppSettingsBase{IAppSettingsSource}" />
public abstract class AppSettingsBase : ReadOnlyAppSettingsBase<IAppSettingsSource>
{
	#region Constructors
	/// <summary>
	/// Initializes a new instance of the <see cref="AppSettingsBase"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="cache">The cache.</param>
	/// <param name="appSettingsSource">The application settings source.</param>
	/// <param name="genericTypeConverter">The generic type converter.</param>
	protected AppSettingsBase(ILogger logger,
		IMemoryCache cache,
		IAppSettingsSource appSettingsSource,
		IGenericTypeConverter genericTypeConverter)
		: base(logger, cache, appSettingsSource, genericTypeConverter)
	{
	}
	#endregion

	#region Protected Methods		
	/// <summary>
	/// Sets the setting with the specifed <paramref name="key"/>.
	/// </summary>
	/// <typeparam name="T">The type of the value being set.</typeparam>
	/// <param name="value">The value.</param>
	/// <param name="key">The key.</param>
	protected virtual void SetSetting<T>(T value, [CallerMemberName] string key = "")
	{
		try
		{
			Guard.IsNotNullOrWhiteSpace(key, nameof(key));

			string? valueToStore = null;

			if (value is not null)
			{
				Type type = typeof(T);

				if (type.IsPrimitive || type.IsEnum || type == typeof(string))
				{
					valueToStore = value.ToString()?.Trim();

					if (string.IsNullOrEmpty(valueToStore))
						valueToStore = null;
				}
				else
				{
					valueToStore = ToJson(value);
				}
			}

			AppSettingsSource.SetValue(key, valueToStore);
			Cache.Remove(GenerateCacheKey(key));
		}
		catch (Exception exc) when (Logger.WriteError(exc))
		{
			throw;
		}
	}

	/// <summary>
	/// Serializes the specified <paramref name="value"/> as JSON.
	/// </summary>
	/// <param name="value">The value.</param>
	/// <returns>The JSON string.</returns>
	protected virtual string ToJson(object value) => UmbrellaStatics.SerializeJson(value);
	#endregion
}