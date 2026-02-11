


using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Umbrella.Utilities.Caching.Abstractions;
using Umbrella.Utilities.Constants;
using Umbrella.Utilities.Exceptions;
using Umbrella.Utilities.Extensions;
using Umbrella.Utilities.Hosting.Options;

namespace Umbrella.Utilities.Hosting;

// TODO: Write unit tests for this and fix issues with paths with slashes, etc.
/// <summary>
/// An implementation of the <see cref="UmbrellaHostingEnvironment" /> type for use with console applications.
/// </summary>
/// <seealso cref="UmbrellaHostingEnvironment" />
public class UmbrellaConsoleHostingEnvironment : UmbrellaHostingEnvironment
{
	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaConsoleHostingEnvironment"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="options">The options.</param>
	/// <param name="cache">The cache.</param>
	/// <param name="cacheKeyUtility">The cache key utility.</param>
	public UmbrellaConsoleHostingEnvironment(
		ILogger<UmbrellaConsoleHostingEnvironment> logger,
		UmbrellaConsoleHostingEnvironmentOptions options,
		IHybridCache cache,
		ICacheKeyUtility cacheKeyUtility)
		: base(logger, options, cache, cacheKeyUtility)
	{
		var fileProviderLazy = new Lazy<IFileProvider>(() => new PhysicalFileProvider(options.BaseDirectory));

		FileProvider = fileProviderLazy;
	}

	/// <inheritdoc />
	public override string? MapPath(string virtualPath)
	{
		Guard.IsNotNullOrWhiteSpace(virtualPath, nameof(virtualPath));

		try
		{
			string key = CacheKeyUtility.Create<UmbrellaConsoleHostingEnvironment>([virtualPath]);

			return Cache.GetOrCreate(key, () =>
			{
				string cleanedPath = TransformPathForFileProvider(virtualPath);

				return FileProvider.Value.GetFileInfo(cleanedPath)?.PhysicalPath;
			},
			Options);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { virtualPath }))
		{
			throw new UmbrellaException("There was a problem mapping the path.", exc);
		}
	}

	/// <inheritdoc />
	protected override string TransformPathForFileProvider(string virtualPath)
	{
		ReadOnlySpan<char> span = virtualPath.AsSpan();

		if (span[0] == '~')
			span = span.TrimStart('~');

		if (span[0] != '/')
		{
			int newLength = span.Length + 1;
			Span<char> newSpan = newLength <= StackAllocConstants.MaxCharSize ? stackalloc char[newLength] : new char[newLength];
			newSpan[0] = '/';
			_ = newSpan.Write(1, span);

			return newSpan.ToString();
		}

		return span.ToString();
	}
}