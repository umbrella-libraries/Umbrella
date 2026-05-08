using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Umbrella.Utilities.Options.Abstractions;

namespace Umbrella.FileSystem.Abstractions;

/// <summary>
/// This is a base class for more specific options types.
/// It is used by the <see cref="UmbrellaFileStorageProvider{TFileInfo, TOptions}"/> type as a way of generically specifying options without having to resort to generics.
/// </summary>
public abstract class UmbrellaFileStorageProviderOptionsBase : IServicesResolverUmbrellaOptions, IUmbrellaFileStorageProviderOptions
{
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
	}
}
