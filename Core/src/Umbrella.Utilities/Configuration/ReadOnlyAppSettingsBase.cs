


using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Umbrella.Utilities.Configuration.Abstractions;
using Umbrella.Utilities.TypeConverters.Abstractions;

namespace Umbrella.Utilities.Configuration;

/// <summary>
/// The base class for an AppSettings class that contains property definitions for settings that are read from the appSettings section
/// of the application config file, e.g. app.config, web.config.
/// </summary>
/// <seealso cref="ReadOnlyAppSettingsBase{IReadOnlyAppSettingsSource}"/>
public abstract class ReadOnlyAppSettingsBase : ReadOnlyAppSettingsBase<IReadOnlyAppSettingsSource>
{
	#region Constructors		
	/// <summary>
	/// Initializes a new instance of the <see cref="ReadOnlyAppSettingsBase"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="cache">The cache.</param>
	/// <param name="appSettingsSource">The application settings source.</param>
	/// <param name="genericTypeConverter">The generic type converter.</param>
	protected ReadOnlyAppSettingsBase(ILogger logger,
		IMemoryCache cache,
		IReadOnlyAppSettingsSource appSettingsSource,
		IGenericTypeConverter genericTypeConverter)
		: base(logger, cache, appSettingsSource, genericTypeConverter)
	{
	}
	#endregion
}

/// <summary>
/// The base class for an AppSettings class that contains property definitions for settings that are read from
/// </summary>
/// <typeparam name="TAppSettingsSource">The type of the application settings source.</typeparam>
/// <seealso cref="IReadOnlyAppSettingsSource"/>
public abstract class ReadOnlyAppSettingsBase<TAppSettingsSource>
	where TAppSettingsSource : IReadOnlyAppSettingsSource
{
	#region Private Static Members
	private static readonly MemoryCacheEntryOptions _defaultMemoryCacheEntryOptions = new();
	private static readonly string _cacheKeyPrefix = typeof(ReadOnlyAppSettingsBase<TAppSettingsSource>).FullName ?? "";
	#endregion

	#region Protected Properties		
	/// <summary>
	/// Gets the log.
	/// </summary>
	protected ILogger Logger { get; }

	/// <summary>
	/// Gets the cache.
	/// </summary>
	protected IMemoryCache Cache { get; }

	/// <summary>
	/// Gets the application settings source.
	/// </summary>
	protected TAppSettingsSource AppSettingsSource { get; }

	/// <summary>
	/// Gets the generic type converter.
	/// </summary>
	protected IGenericTypeConverter GenericTypeConverter { get; }
	#endregion

	#region Constructors		
	/// <summary>
	/// Initializes a new instance of the <see cref="ReadOnlyAppSettingsBase{TAppSettingsSource}"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="cache">The cache.</param>
	/// <param name="appSettingsSource">The application settings source.</param>
	/// <param name="genericTypeConverter">The generic type converter.</param>
	protected ReadOnlyAppSettingsBase(
		ILogger logger,
		IMemoryCache cache,
		TAppSettingsSource appSettingsSource,
		IGenericTypeConverter genericTypeConverter)
	{
		Logger = logger;
		Cache = cache;
		AppSettingsSource = appSettingsSource;
		GenericTypeConverter = genericTypeConverter;
	}
	#endregion

	#region Protected Methods
	// Move to Options class		
	/// <summary>
	/// Generates the cache key.
	/// </summary>
	/// <param name="settingKey">The setting key.</param>
	/// <returns>The cache key.</returns>
	protected virtual string GenerateCacheKey(string settingKey) => $"{_cacheKeyPrefix}:{settingKey}";

	/// <summary>
	/// Gets the cache entry options builder. Defaults to <see langword="null" /> unless overridden in a derived type.
	/// </summary>
	/// <returns>The cache entry options builder.</returns>
	protected virtual Func<MemoryCacheEntryOptions>? GetCacheEntryOptionsFunc() => null;

	/// <summary>
	/// Converts the specified JSON string to the specified type <typeparamref name="T"/>.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <param name="value">The value.</param>
	/// <returns>An instance of type <typeparamref name="T"/>.</returns>
	protected virtual T? FromJson<T>(string value) => UmbrellaStatics.DeserializeJson<T>(value);

	/// <summary>
	/// Gets the setting.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <param name="fallback">The fallback.</param>
	/// <param name="key">The key.</param>
	/// <param name="useCache">if set to <see langword="true"/>, stores the item in the cache.</param>
	/// <param name="throwException">if set to <see langword="true"/>, thows an exception if the app setting could not be found, otherwise the <paramref name="fallback"/> is returned.</param>
	/// <param name="customValueConverter">The custom value converter.</param>
	/// <returns>The app setting value or the fallback.</returns>
	protected virtual T? GetSetting<T>(T fallback = default!, [CallerMemberName] string key = "", bool useCache = true, bool throwException = false, Func<string?, T>? customValueConverter = null)
	{
		Guard.IsNotNullOrWhiteSpace(key, nameof(key));

		try
		{
			T GetValue()
			{
				string? value = AppSettingsSource.GetValue(key);

				return string.IsNullOrWhiteSpace(value) && throwException
					? throw new ArgumentException($"The value for key: {key} is not valid. An app setting with that key cannot be found.")
					: GenericTypeConverter.Convert(value, fallback, customValueConverter);
			}

			return useCache
				? Cache.GetOrCreate(GenerateCacheKey(key), entry =>
				{
					_ = entry.SetOptions(GetCacheEntryOptionsFunc()?.Invoke() ?? _defaultMemoryCacheEntryOptions);
					return GetValue();
				})
				: GetValue();
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { key, useCache, throwException }))
		{
			throw;
		}
	}

	/// <summary>
	/// Gets the setting with the specified <paramref name="key"/>.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <param name="fallbackCreator">The fallback creator.</param>
	/// <param name="key">The key.</param>
	/// <param name="useCache">if set to <see langword="true"/>, stores the item in the cache.</param>
	/// <param name="throwException">if set to <see langword="true"/>, thows an exception if the app setting could not be found, otherwise the <paramref name="fallbackCreator"/> is used to build the returned value.</param>
	/// <param name="customValueConverter">The custom value converter.</param>
	/// <returns>The app setting value or the fallback.</returns>
	protected virtual T? GetSetting<T>(Func<T> fallbackCreator, [CallerMemberName] string key = "", bool useCache = true, bool throwException = false, Func<string?, T>? customValueConverter = null)
	{
		Guard.IsNotNullOrWhiteSpace(key, nameof(key));

		try
		{
			T GetValue()
			{
				string? value = AppSettingsSource.GetValue(key);

				return string.IsNullOrWhiteSpace(value) && throwException
					? throw new ArgumentException($"The value for key: {key} is not valid. An app setting with that key cannot be found.")
					: GenericTypeConverter.Convert(value, fallbackCreator, customValueConverter);
			}

			return useCache
				? Cache.GetOrCreate(GenerateCacheKey(key), entry =>
				{
					_ = entry.SetOptions(GetCacheEntryOptionsFunc()?.Invoke() ?? _defaultMemoryCacheEntryOptions);
					return GetValue();
				})
				: GetValue();
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { key, useCache, throwException }))
		{
			throw;
		}
	}

	/// <summary>
	/// Gets the enum setting with the specified <paramref name="key"/>.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <param name="fallback">The fallback.</param>
	/// <param name="key">The key.</param>
	/// <param name="useCache">if set to <see langword="true"/>, stores the item in the cache.</param>
	/// <param name="throwException">if set to <see langword="true"/>, thows an exception if the app setting could not be found, otherwise the <paramref name="fallback"/> is returned.</param>
	/// <returns>The value of the app setting or the <paramref name="fallback"/>.</returns>
	protected virtual T GetSettingEnum<T>(T fallback = default, [CallerMemberName] string key = "", bool useCache = true, bool throwException = false)
		where T : struct, Enum
	{
		Guard.IsNotNullOrWhiteSpace(key, nameof(key));

		try
		{
			T GetValue()
			{
				string? value = AppSettingsSource.GetValue(key);

				return string.IsNullOrWhiteSpace(value) && throwException
					? throw new ArgumentException($"The value for key: {key} is not valid. An app setting with that key cannot be found.")
					: GenericTypeConverter.ConvertToEnum(value, fallback);
			}

			return useCache
				? Cache.GetOrCreate(GenerateCacheKey(key), entry =>
				{
					_ = entry.SetOptions(GetCacheEntryOptionsFunc()?.Invoke() ?? _defaultMemoryCacheEntryOptions);
					return GetValue();
				})
				: GetValue();
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { key, useCache, throwException }))
		{
			throw;
		}
	}
	#endregion
}