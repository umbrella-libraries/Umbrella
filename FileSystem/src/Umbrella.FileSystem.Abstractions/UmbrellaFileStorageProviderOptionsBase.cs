
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Umbrella.Utilities.Constants;
using Umbrella.Utilities.Extensions;
using Umbrella.Utilities.Options.Abstractions;

namespace Umbrella.FileSystem.Abstractions;

/// <summary>
/// This is a base class for more specific options types.
/// It is used by the <see cref="UmbrellaFileStorageProvider{TFileInfo, TOptions}"/> type as a way of generically specifying options without having to resort to generics.
/// </summary>
public abstract class UmbrellaFileStorageProviderOptionsBase : IServicesResolverUmbrellaOptions, IUmbrellaFileStorageProviderOptions
{
	private readonly object _syncRoot = new();
	private IServiceCollection? _services;
	private IServiceProvider? _serviceProvider;
	private Dictionary<string, IUmbrellaFileAuthorizationHandler>? _authorizationHandlerMappings;

	/// <summary>
	/// Gets or sets a value indicating whether access to files that do not have a registered <see cref="IUmbrellaFileAuthorizationHandler"/> should be permitted.
	/// </summary>
	/// <remarks>Defaults to <see langword="false"/>.</remarks>
	public bool AllowUnhandledFileAuthorizationChecks { get; set; }

	/// <inheritdoc/>
	public string TempFilesDirectoryName { get; set; } = UmbrellaFileSystemConstants.DefaultTempFilesDirectoryName;

	/// <inheritdoc/>
	public string WebFilesDirectoryName { get; set; } = UmbrellaFileSystemConstants.DefaultWebFilesDirectoryName;

	/// <inheritdoc/>
	public void Initialize(IServiceCollection services, IServiceProvider serviceProvider)
	{
		Guard.IsNotNull(services);
		Guard.IsNotNull(serviceProvider);

		_services = services;
		_serviceProvider = serviceProvider;
	}

	/// <summary>
	/// Gets the authorization handler for the specified <paramref name="fileInfo"/>.
	/// </summary>
	/// <param name="fileInfo">The file.</param>
	/// <returns>The handler.</returns>
	/// <exception cref="UmbrellaFileSystemException">An {nameof(IUmbrellaFileAuthorizationHandler)} could not be found for the specified directory name '{directoryNameLowered}'. Please ensure it has been registered with the application's DI container.</exception>
	public IUmbrellaFileAuthorizationHandler? GetAuthorizationHandler(IUmbrellaFileInfo fileInfo)
	{
		Guard.IsNotNull(fileInfo);
		Guard.IsNotNull(_services);
		Guard.IsNotNull(_serviceProvider);

		ReadOnlySpan<char> fileSubPathSpan = fileInfo.SubPath.AsSpan();
		fileSubPathSpan = fileSubPathSpan.TrimStart('/');

		int idxFirstSlash = fileSubPathSpan.IndexOf('/');
		fileSubPathSpan = fileSubPathSpan[..idxFirstSlash];

		Span<char> directoryNameSpanLowered = fileSubPathSpan.Length <= StackAllocConstants.MaxCharSize
			? stackalloc char[fileSubPathSpan.Length]
			: new char[fileSubPathSpan.Length];

		fileSubPathSpan.ToLowerInvariantSlim(directoryNameSpanLowered);

		string directoryNameLowered = directoryNameSpanLowered.ToString();

		if (_authorizationHandlerMappings is null)
		{
			lock (_syncRoot)
			{
				_authorizationHandlerMappings ??= _services
					.Where(x => typeof(IUmbrellaFileAuthorizationHandler).IsAssignableFrom(x.ServiceType) && x.ImplementationType is not null)
					.Select(x => _serviceProvider.GetRequiredService(x.ServiceType!))
					.Cast<IUmbrellaFileAuthorizationHandler>()
					.ToDictionary(x => x.DirectoryName.ToLowerInvariant(), x => x);
			}
		}

		return _authorizationHandlerMappings.TryGetValue(directoryNameLowered, out IUmbrellaFileAuthorizationHandler? handler) ? handler : null;
	}
}