using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Umbrella.DataAccess.Abstractions;
using Umbrella.Utilities.Caching.Abstractions;
using Umbrella.Utilities.Primitives;
using Umbrella.WebUtilities.Versioning.Abstractions;
using Umbrella.WebUtilities.Versioning.Models;
using Umbrella.WebUtilities.Versioning.Options;

namespace Umbrella.WebUtilities.Versioning;

/// <summary>
/// A service used to obtain version information about the system.
/// </summary>
/// <seealso cref="ISystemVersionService" />
public class SystemVersionService : ISystemVersionService
{
	private readonly ILogger _logger;
	private readonly IServiceProvider _serviceProvider;
	private readonly IHybridCache _cache;
	private readonly ICacheKeyUtility _cacheKeyUtility;
	private readonly SystemVersionServiceOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="SystemVersionService"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="serviceProvider">The service provider.</param>
	/// <param name="cache">The cache.</param>
	/// <param name="cacheKeyUtility">The cache key utility.</param>
	/// <param name="options">The options.</param>
	public SystemVersionService(
		ILogger<SystemVersionService> logger,
		IServiceProvider serviceProvider,
		IHybridCache cache,
		ICacheKeyUtility cacheKeyUtility,
		SystemVersionServiceOptions options)
	{
		_logger = logger;
		_serviceProvider = serviceProvider;
		_cache = cache;
		_cacheKeyUtility = cacheKeyUtility;
		_options = options;
	}

	/// <inheritdoc />
	public async ValueTask<SystemVersionModel> GetAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			string cacheKey = _cacheKeyUtility.Create<SystemVersionService>("Version");

			return await _cache.GetOrCreateAsync(cacheKey, async () =>
			{
				string versionFilePath = _options.VersionFilePath;
				string? version = null;

				if (File.Exists(versionFilePath))
				{
					static string SanitizeVersionNumber(string version)
					{
						if (version.Length < 8)
							return version;

						Span<char> chars = stackalloc char[8];

						for (int i = 0; i < 8; i++)
						{
							chars[i] = char.ToLowerInvariant(version[i]);
						}

						return chars.ToString();
					}

					using StreamReader reader = new(versionFilePath);
					version = await reader.ReadToEndAsync().ConfigureAwait(false);
					version = SanitizeVersionNumber(version);
				}

				version ??= (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly())?.GetName()?.Version?.ToString() ?? "Unavailable";

				string? databaseVersion = null;

				if (_options.IncludeDatabaseVersion)
				{
					using IServiceScope serviceScope = _serviceProvider.CreateScope();

					var dbRepository = serviceScope.ServiceProvider.GetRequiredService<IDatabaseVersionRepository>();
					databaseVersion = await dbRepository.GetDatabaseVersionAsync(cancellationToken).ConfigureAwait(false);
				}

				return new SystemVersionModel(version, databaseVersion);
			},
			_options,
			() => new[] { new PhysicalFileChangeToken(new FileInfo(_options.VersionFilePath)) },
			cancellationToken);
		}
		catch (Exception exc) when (_logger.WriteError(exc))
		{
			throw new UmbrellaWebException("There was a problem getting version information.", exc);
		}
	}
}