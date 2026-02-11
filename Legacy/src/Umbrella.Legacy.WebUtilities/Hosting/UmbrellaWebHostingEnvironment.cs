
using System.Buffers;
using System.ComponentModel;
using System.Text;
using System.Web;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Umbrella.Utilities.Caching.Abstractions;
using Umbrella.Utilities.Constants;
using Umbrella.Utilities.Hosting;
using Umbrella.Utilities.Hosting.Options;
using Umbrella.WebUtilities.Exceptions;
using Umbrella.WebUtilities.Hosting;

namespace Umbrella.Legacy.WebUtilities.Hosting;

/// <summary>
/// An implementation of the <see cref="IUmbrellaWebHostingEnvironment" /> for use with ASP.NET applications.
/// </summary>
/// <seealso cref="UmbrellaHostingEnvironment" />
/// <seealso cref="IUmbrellaWebHostingEnvironment" />
public class UmbrellaWebHostingEnvironment : UmbrellaHostingEnvironment, IUmbrellaWebHostingEnvironment
{
	#region Constructors
	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaWebHostingEnvironment"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="options">The options.</param>
	/// <param name="cache">The cache.</param>
	/// <param name="cacheKeyUtility">The cache key utility.</param>
	public UmbrellaWebHostingEnvironment(
		ILogger<UmbrellaWebHostingEnvironment> logger,
		UmbrellaHostingEnvironmentOptions options,
		IHybridCache cache,
		ICacheKeyUtility cacheKeyUtility)
		: base(logger, options, cache, cacheKeyUtility)
	{
		var fileProviderLazy = new Lazy<IFileProvider>(() => new PhysicalFileProvider(System.Web.Hosting.HostingEnvironment.MapPath("~/")));

		FileProvider = fileProviderLazy;
	}
	#endregion

	#region IUmbrellaHostingEnvironment Members
	/// <inheritdoc />
	public override string? MapPath(string virtualPath) => MapPath(virtualPath, true);
	#endregion

	#region IUmbrellaWebHostingEnvironment Members
	/// <inheritdoc />
	public virtual string? MapPath(string virtualPath, bool fromContentRoot)
	{
		Guard.IsNotNullOrWhiteSpace(virtualPath, nameof(virtualPath));

		string[]? cacheKeyParts = null;

		try
		{
			cacheKeyParts = ArrayPool<string>.Shared.Rent(2);
			cacheKeyParts[0] = virtualPath;
			cacheKeyParts[1] = fromContentRoot.ToString();

			string key = CacheKeyUtility.Create<UmbrellaWebHostingEnvironment>(cacheKeyParts, 2);

			return Cache.GetOrCreate(key, () =>
			{
				if (virtualPath == "~/")
					return System.Web.Hosting.HostingEnvironment.MapPath(virtualPath);

				string cleanedPath = TransformPathForFileProvider(virtualPath);

				return FileProvider.Value.GetFileInfo(cleanedPath)?.PhysicalPath;
			},
			Options);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { virtualPath, fromContentRoot }))
		{
			throw new UmbrellaWebException("There has been a problem mapping the specified virtual path.", exc);
		}
		finally
		{
			if (cacheKeyParts is not null)
				ArrayPool<string>.Shared.Return(cacheKeyParts);
		}
	}

	/// <inheritdoc />
	public virtual string MapWebPath(string virtualPath, bool toAbsoluteUrl = false, string scheme = "https", bool appendVersion = false, string versionParameterName = "v", bool mapFromContentRoot = true, bool watchWhenAppendVersion = true, string? pathBaseOverride = null, string? hostOverride = null)
	{
		Guard.IsNotNullOrWhiteSpace(virtualPath, nameof(virtualPath));
		Guard.IsNotNullOrWhiteSpace(scheme, nameof(scheme));
		Guard.IsNotNullOrWhiteSpace(versionParameterName, nameof(versionParameterName));

		string[]? cacheKeyParts = null;

		try
		{
			cacheKeyParts = ArrayPool<string>.Shared.Rent(7);
			cacheKeyParts[0] = virtualPath;
			cacheKeyParts[1] = toAbsoluteUrl.ToString();
			cacheKeyParts[2] = scheme;
			cacheKeyParts[3] = appendVersion.ToString();
			cacheKeyParts[4] = versionParameterName;
			cacheKeyParts[5] = mapFromContentRoot.ToString();
			cacheKeyParts[6] = watchWhenAppendVersion.ToString();

			string key = CacheKeyUtility.Create<UmbrellaWebHostingEnvironment>(cacheKeyParts, 7);
			string cleanedPath = TransformPathForFileProvider(virtualPath);

			return Cache.GetOrCreate(key, () =>
			{
				string virtualApplicationPath = pathBaseOverride ?? (HttpRuntime.AppDomainAppVirtualPath != "/"
					? HttpRuntime.AppDomainAppVirtualPath
					: "");

				//Prefix the path with the virtual application segment but only if the cleanedPath doesn't already start with the segment
				string url = cleanedPath.StartsWith(virtualApplicationPath, StringComparison.OrdinalIgnoreCase)
					? cleanedPath
					: virtualApplicationPath + cleanedPath;

				if (toAbsoluteUrl)
					url = $"{scheme}://{ResolveHttpHost(hostOverride)}{url}";

				if (appendVersion)
				{
					IFileInfo fileInfo = FileProvider.Value.GetFileInfo(cleanedPath);

					if (!fileInfo.Exists)
						throw new FileNotFoundException($"The specified virtual path {virtualPath} does not exist on disk at {fileInfo.PhysicalPath}.");

					long versionHash = fileInfo.LastModified.UtcDateTime.ToFileTimeUtc() ^ fileInfo.Length;
					string version = Convert.ToString(versionHash, 16);

					string qsStart = url.Contains("?") ? "&" : "?";

					url = $"{url}{qsStart}{versionParameterName}={version}";
				}

				return url;
			},
			Options,
			() => appendVersion && watchWhenAppendVersion ? new[] { FileProvider.Value.Watch(cleanedPath) } : null);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { virtualPath, toAbsoluteUrl, scheme, appendVersion, versionParameterName, mapFromContentRoot, watchWhenAppendVersion }))
		{
			throw new UmbrellaWebException("There has been a problem mapping the specified virtual path.", exc);
		}
		finally
		{
			if (cacheKeyParts is not null)
				ArrayPool<string>.Shared.Return(cacheKeyParts);
		}
	}

	/// <inheritdoc />
	public async Task<string?> GetFileContentAsync(string virtualPath, bool fromContentRoot, bool cache = true, bool watch = true, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNullOrWhiteSpace(virtualPath, nameof(virtualPath));

		try
		{
			return fromContentRoot
				? await GetFileContentAsync("Standard", FileProvider.Value, virtualPath, cache, watch, cancellationToken).ConfigureAwait(false)
				: await GetFileContentAsync("Web", FileProvider.Value, virtualPath, cache, watch, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { virtualPath, cache, watch }))
		{
			throw new UmbrellaWebException("There has been a problem reading the contents of the specified file.", exc);
		}
	}
	#endregion

	#region Protected Methods		
	/// <summary>
	/// Resolves the HTTP host of the current request.
	/// </summary>
	/// <param name="hostOverride">The value used to override the hostname from the current HttpContext.</param>
	/// <returns>The HTTP host for the current request.</returns>
	protected virtual string ResolveHttpHost(string? hostOverride) => hostOverride ?? HttpContext.Current.Request.Url.Host;

	/// <inheritdoc />
	protected override string TransformPathForFileProvider(string virtualPath) => TransformPath(virtualPath, false, true, true);
	#endregion

	#region Internal Methods
	internal string TransformPath(string virtualPath, bool ensureStartsWithTildeSlash, bool ensureNoTilde, bool ensureLeadingSlash)
	{
		ReadOnlySpan<char> span = virtualPath.AsSpan().Trim();

		int length = span.Length;

		if (ensureNoTilde && span[0] == '~')
			span = span.TrimStart('~');

		bool leadingSlashToInsert = false;

		if (ensureLeadingSlash && span[0] != '/')
			leadingSlashToInsert = true;

		bool leadingTildaToInsert = false;
		bool leadingTildaSlashToInsert = false;

		if (ensureStartsWithTildeSlash && !span.StartsWith("~/".AsSpan(), StringComparison.Ordinal))
		{
			if (span[0] == '/')
				leadingTildaToInsert = true;
			else
				leadingTildaSlashToInsert = true;
		}

		int duplicateSlashCount = 0;

		if (leadingTildaSlashToInsert)
		{
			int newLength = span.Length + 2;

			Span<char> outputSpan = newLength <= StackAllocConstants.MaxCharSize ? stackalloc char[newLength] : new char[newLength];
			outputSpan[0] = '~';
			outputSpan[1] = '/';
			span.CopyTo(outputSpan.Slice(2));

			for (int i = 0; i < outputSpan.Length; i++)
			{
				if (i > 0 && outputSpan[i] == '/' && outputSpan[i - 1] == '/')
				{
					duplicateSlashCount++;
				}
			}

			if (duplicateSlashCount > 0)
			{
				int duplicateSlashCountNewLength = outputSpan.Length - duplicateSlashCount;

				Span<char> cleanedSpan = duplicateSlashCountNewLength <= StackAllocConstants.MaxCharSize ? stackalloc char[duplicateSlashCountNewLength] : new char[duplicateSlashCountNewLength];

				int j = 0;
				for (int i = 0; i < outputSpan.Length; i++)
				{
					if (i > 0 && outputSpan[i] == '/' && outputSpan[i - 1] == '/')
					{
						continue;
					}

					cleanedSpan[j++] = outputSpan[i];
				}

				return cleanedSpan.ToString();
			}

			return outputSpan.ToString();
		}
		else if (leadingTildaToInsert)
		{
			int newLength = span.Length + 1;

			Span<char> outputSpan = newLength <= StackAllocConstants.MaxCharSize ? stackalloc char[newLength] : new char[newLength];
			outputSpan[0] = '~';
			span.CopyTo(outputSpan.Slice(1));

			for (int i = 0; i < outputSpan.Length; i++)
			{
				if (i > 0 && outputSpan[i] == '/' && outputSpan[i - 1] == '/')
				{
					duplicateSlashCount++;
				}
			}

			if (duplicateSlashCount > 0)
			{
				int duplicateSlashCountNewLength = outputSpan.Length - duplicateSlashCount;

				Span<char> cleanedSpan = duplicateSlashCountNewLength <= StackAllocConstants.MaxCharSize ? stackalloc char[duplicateSlashCountNewLength] : new char[duplicateSlashCountNewLength];

				int j = 0;
				for (int i = 0; i < outputSpan.Length; i++)
				{
					if (i > 0 && outputSpan[i] == '/' && outputSpan[i - 1] == '/')
					{
						continue;
					}

					cleanedSpan[j++] = outputSpan[i];
				}

				return cleanedSpan.ToString();
			}

			return outputSpan.ToString();
		}
		else if (leadingSlashToInsert)
		{
			int newLength = span.Length + 1;

			Span<char> outputSpan = newLength <= StackAllocConstants.MaxCharSize ? stackalloc char[newLength] : new char[newLength];
			outputSpan[0] = '/';
			span.CopyTo(outputSpan.Slice(1));

			for (int i = 0; i < outputSpan.Length; i++)
			{
				if (i > 0 && outputSpan[i] == '/' && outputSpan[i - 1] == '/')
				{
					duplicateSlashCount++;
				}
			}

			if (duplicateSlashCount > 0)
			{
				int duplicateSlashCountNewLength = outputSpan.Length - duplicateSlashCount;

				Span<char> cleanedSpan = duplicateSlashCountNewLength <= StackAllocConstants.MaxCharSize ? stackalloc char[duplicateSlashCountNewLength] : new char[duplicateSlashCountNewLength];

				int j = 0;
				for (int i = 0; i < outputSpan.Length; i++)
				{
					if (i > 0 && outputSpan[i] == '/' && outputSpan[i - 1] == '/')
					{
						continue;
					}

					cleanedSpan[j++] = outputSpan[i];
				}

				return cleanedSpan.ToString();
			}

			return outputSpan.ToString();
		}

		for (int i = 0; i < span.Length; i++)
		{
			if (i > 0 && span[i] == '/' && span[i - 1] == '/')
			{
				duplicateSlashCount++;
			}
		}

		if (duplicateSlashCount > 0)
		{
			int duplicateSlashCountNewLength = span.Length - duplicateSlashCount;

			Span<char> cleanedSpan = duplicateSlashCountNewLength <= StackAllocConstants.MaxCharSize ? stackalloc char[duplicateSlashCountNewLength] : new char[duplicateSlashCountNewLength];

			int j = 0;
			for (int i = 0; i < span.Length; i++)
			{
				if (i > 0 && span[i] == '/' && span[i - 1] == '/')
				{
					continue;
				}

				cleanedSpan[j++] = span[i];
			}

			return cleanedSpan.ToString();
		}

		return span.ToString();
	}

	[Obsolete]
	[EditorBrowsable(EditorBrowsableState.Advanced)]
	internal string TransformPathOld(string virtualPath, bool ensureStartsWithTildeSlash, bool ensureNoTilde, bool ensureLeadingSlash)
	{
		StringBuilder sb = new StringBuilder(virtualPath)
			.Trim();

		if (ensureNoTilde && sb.StartsWith("~", StringComparison.Ordinal))
			_ = sb.Remove(0, 1);

		if (ensureLeadingSlash && !sb.StartsWith('/'))
			_ = sb.Insert(0, '/');

		if (ensureStartsWithTildeSlash && !sb.StartsWith("~/", StringComparison.Ordinal))
		{
			if (sb.StartsWith('/'))
				_ = sb.Insert(0, '~');
			else
				_ = sb.Insert(0, "~/");
		}

		for (int i = 0; i < sb.Length; i++)
		{
			if (i > 0 && sb[i] == '/' && sb[i - 1] == '/')
				_ = sb.Remove(i--, 1);
		}

		return sb.ToString();
	}
	#endregion
}