
using System.Globalization;
using System.Text.RegularExpressions;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Logging;
using Umbrella.Utilities.Constants;
using Umbrella.Utilities.Extensions;

namespace Umbrella.DynamicImage.Abstractions;

/// <summary>
/// Contains utility methods for common operations performed by the Dynamic Image infrastructure.
/// </summary>
/// <seealso cref="IDynamicImageUtility" />
public partial class DynamicImageUtility : IDynamicImageUtility
{
	private const string VirtualPathFormat = "~/{0}/{1}/{2}/{3}/{4}/{5}";
	private const string VersionedVirtualPathFormat = "~/{0}/{1}/{2}/{3}/{4}/{5}/{6}";

	private static readonly (DynamicImageParseUrlResult, DynamicImageOptions) _invalidParseUrlResult = (DynamicImageParseUrlResult.Invalid, default);
	private static readonly (DynamicImageParseUrlResult, DynamicImageOptions) _skipParseUrlResult = (DynamicImageParseUrlResult.Skip, default);
	private static readonly Regex _densityRegex = CreateDensityRegex();
	private static readonly char[] _segmentSeparatorArray = ['/'];

	/// <summary>
	/// Gets the logger.
	/// </summary>
	protected ILogger Logger { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="DynamicImageUtility"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	public DynamicImageUtility(ILogger<DynamicImageUtility> logger)
	{
		Logger = logger;
	}

	/// <inheritdoc />
	public virtual DynamicImageFormat ParseImageFormat(string format)
	{
		Guard.IsNotNullOrWhiteSpace(format, nameof(format));

		try
		{
			ReadOnlySpan<char> formatSpan = format.AsSpan().TrimStart('.').Trim();
			Span<char> target = formatSpan.Length <= StackAllocConstants.MaxCharSize ? stackalloc char[formatSpan.Length] : new char[formatSpan.Length];
			formatSpan.ToLowerInvariantSlim(target);

			return target switch
			{
				var _ when target.SequenceEqual("png".AsSpan()) => DynamicImageFormat.Png,
				var _ when target.SequenceEqual("bmp".AsSpan()) => DynamicImageFormat.Bmp,
				var _ when target.SequenceEqual("jpg".AsSpan()) => DynamicImageFormat.Jpeg,
				var _ when target.SequenceEqual("jpeg".AsSpan()) => DynamicImageFormat.Jpeg,
				var _ when target.SequenceEqual("gif".AsSpan()) => DynamicImageFormat.Gif,
				var _ when target.SequenceEqual("webp".AsSpan()) => DynamicImageFormat.WebP,
				var _ when target.SequenceEqual("avif".AsSpan()) => DynamicImageFormat.Avif,
				_ => DynamicImageFormat.Jpeg
			};
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { format }))
		{
			throw new UmbrellaDynamicImageException("There has been a problem parsing the image format.", exc);
		}
	}

	/// <inheritdoc />
	public virtual (DynamicImageParseUrlResult status, DynamicImageOptions imageOptions) TryParseUrl(string dynamicImagePathPrefix, string relativeUrl, DynamicImageFormat? overrideFormat = null)
	{
		Guard.IsNotNullOrWhiteSpace(dynamicImagePathPrefix);
		Guard.IsNotNullOrWhiteSpace(relativeUrl);

		try
		{
			string url = relativeUrl.TrimToLowerInvariant();

			// Extract focal point before stripping the query string.
			double? focalPointX = null;
			double? focalPointY = null;

#if NET6_0_OR_GREATER
			int qsIdx = url.IndexOf('?', StringComparison.Ordinal);
#else
			int qsIdx = url.IndexOf('?');
#endif
			if (qsIdx >= 0)
			{
				(focalPointX, focalPointY) = ParseFocalPointFromQueryString(url.AsSpan(qsIdx + 1));
				url = url[..qsIdx];
			}

			if (!Path.HasExtension(url))
				return (DynamicImageParseUrlResult.Invalid, default);

			string pathPrefix = dynamicImagePathPrefix.TrimToLowerInvariant();

			if (string.IsNullOrEmpty(url) || !url.StartsWith($"/{pathPrefix}/", StringComparison.Ordinal))
				return _skipParseUrlResult;

			string[] prefixSegments = pathPrefix.Split(_segmentSeparatorArray, StringSplitOptions.RemoveEmptyEntries);
			string[] allSegments = url.Split(_segmentSeparatorArray, StringSplitOptions.RemoveEmptyEntries);

			int relativeSegmentCount = allSegments.Length - prefixSegments.Length;

			if (relativeSegmentCount < 5)
				return _invalidParseUrlResult;

			//Ignore the prefix segments
			int relativeSegmentIndex = prefixSegments.Length;
			_ = int.TryParse(allSegments[relativeSegmentIndex], out int width);
			_ = int.TryParse(allSegments[relativeSegmentIndex + 1], out int height);

			if (width <= 0 || height <= 0)
				return _invalidParseUrlResult;

			DynamicResizeMode mode = allSegments[relativeSegmentIndex + 2].ToEnum<DynamicResizeMode>();
			string originalExtension = allSegments[relativeSegmentIndex + 3];

			int pathStartIndex = relativeSegmentIndex + 4;
			string? versionToken = null;

			if (relativeSegmentCount > 5 && allSegments[pathStartIndex].StartsWith(DynamicImageConstants.VersionTokenPathSegmentPrefix, StringComparison.Ordinal))
			{
				if (!TryParseVersionToken(allSegments[pathStartIndex], out versionToken))
					return _invalidParseUrlResult;

				pathStartIndex++;
			}

			//The extension of this path is the target format the image will be resized as.
			string path = "/" + string.Join("/", allSegments.Skip(pathStartIndex));

			if (!Path.HasExtension(path))
				return _invalidParseUrlResult;

			string targetExtension = Path.GetExtension(path);

			string sourcePath = Path.ChangeExtension(path, "." + originalExtension);

			//Parse the sourcePath for the pixel density information here
			string pathWithoutExtension = Path.GetFileNameWithoutExtension(path);

			//Check to see if the path has a density identifier at the end
			Match densityMatch = _densityRegex.Match(pathWithoutExtension);

			if (densityMatch.Success)
			{
				//Get the density from the 2nd group
				if (densityMatch.Groups.Count is 2 && int.TryParse(densityMatch.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int density))
				{
					int densityIdentifierLength = densityMatch.Value.Length;

					//Remove the density identifier from the path
					string extension = Path.GetExtension(sourcePath);
					int charsToRemove = extension.Length + densityIdentifierLength;

					sourcePath = sourcePath.Remove(sourcePath.Length - charsToRemove, charsToRemove) + extension;

					//Double the dimensions
					width *= density;
					height *= density;
				}
			}

			var imageOptions = new DynamicImageOptions(
				sourcePath,
				width,
				height,
				mode,
				overrideFormat ?? ParseImageFormat(targetExtension),
				focalPointX: focalPointX,
				focalPointY: focalPointY,
				versionToken: versionToken);

			return (DynamicImageParseUrlResult.Success, imageOptions);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { dynamicImagePathPrefix, relativeUrl }))
		{
			return _invalidParseUrlResult;
		}
	}

	/// <inheritdoc />
	public virtual bool ImageOptionsValid(DynamicImageOptions imageOptions, IEnumerable<DynamicImageVariant> validVariants)
	{
		try
		{
			var variant = (DynamicImageVariant)imageOptions;

			return validVariants.Contains(variant);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { imageOptions, validVariants }))
		{
			throw new UmbrellaDynamicImageException("An error has occurred whilst validating the image options.", exc);
		}
	}

	/// <inheritdoc />
	public virtual string GenerateVirtualPath(string dynamicImagePathPrefix, DynamicImageOptions options)
	{
		Guard.IsNotNullOrWhiteSpace(dynamicImagePathPrefix, nameof(dynamicImagePathPrefix));

		try
		{
#if NET6_0_OR_GREATER
			string path = options.SourcePath.Replace("~/", "", StringComparison.Ordinal).TrimStart('/');
#else
			string path = options.SourcePath.Replace("~/", "").TrimStart('/');
#endif

			// Remove the querystring and append to the end of the generated URL.
			string? qs = null;

#if NET6_0_OR_GREATER
			if (path.Contains('?', StringComparison.Ordinal))
#else
			if (path.Contains('?'))
#endif
			{
				string[] parts = path.Split('?');

				if (parts.Length != 2)
					throw new InvalidOperationException("The path contains more than one '?'.");

				path = parts[0];
				qs = parts[1];
			}

			string originalExtension = Path.GetExtension(path).ToLowerInvariant().Remove(0, 1);
			string targetPath = Path.ChangeExtension(path, options.Format.ToFileExtensionString()).ToLowerInvariant();

			string virtualPath;

			if (!string.IsNullOrWhiteSpace(options.VersionToken))
			{
				virtualPath = string.Format(CultureInfo.InvariantCulture,
					VersionedVirtualPathFormat,
					dynamicImagePathPrefix,
					options.Width,
					options.Height,
					options.ResizeMode,
					originalExtension,
					GenerateVersionTokenPathSegment(options.VersionToken!),
					targetPath);
			}
			else
			{
				virtualPath = string.Format(CultureInfo.InvariantCulture,
					VirtualPathFormat,
					dynamicImagePathPrefix,
					options.Width,
					options.Height,
					options.ResizeMode,
					originalExtension,
					targetPath);
			}

			if (!string.IsNullOrEmpty(qs))
				virtualPath += "?" + qs;

			if (options.FocalPointX.HasValue && options.FocalPointY.HasValue)
			{
				string separator = !string.IsNullOrEmpty(qs) ? "&" : "?";
				virtualPath += FormattableString.Invariant($"{separator}fpx={options.FocalPointX.Value:G4}&fpy={options.FocalPointY.Value:G4}");
			}

			return virtualPath;
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { dynamicImagePathPrefix, options }))
		{
			throw new UmbrellaDynamicImageException("An error has occurred whilst generating the virtual path.", exc);
		}
	}

	private static (double? fpx, double? fpy) ParseFocalPointFromQueryString(ReadOnlySpan<char> queryString)
	{
		double? fpx = null;
		double? fpy = null;

		static bool TryParseDouble(ReadOnlySpan<char> span, out double result)
		{
#if NET6_0_OR_GREATER
			return double.TryParse(span, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
#else
			return double.TryParse(span.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out result);
#endif
		}

		ReadOnlySpan<char> remaining = queryString;

		while (!remaining.IsEmpty)
		{
			int ampIdx = remaining.IndexOf('&');
			ReadOnlySpan<char> pair = ampIdx >= 0 ? remaining[..ampIdx] : remaining;
			remaining = ampIdx >= 0 ? remaining[(ampIdx + 1)..] : [];

			int eqIdx = pair.IndexOf('=');
			if (eqIdx < 0)
				continue;

			ReadOnlySpan<char> key = pair[..eqIdx];
			ReadOnlySpan<char> value = pair[(eqIdx + 1)..];

			if (key.Equals("fpx".AsSpan(), StringComparison.OrdinalIgnoreCase))
			{
				if (TryParseDouble(value, out double v))
					fpx = v;
			}
			else if (key.Equals("fpy".AsSpan(), StringComparison.OrdinalIgnoreCase))
			{
				if (TryParseDouble(value, out double v))
					fpy = v;
			}

			if (fpx.HasValue && fpy.HasValue)
				return (fpx, fpy);
		}

		return (fpx, fpy);
	}

	private static string GenerateVersionTokenPathSegment(string versionToken)
	{
		Guard.IsNotNullOrWhiteSpace(versionToken);

		return DynamicImageConstants.VersionTokenPathSegmentPrefix + versionToken.Trim().ToLowerInvariant();
	}

	private static bool TryParseVersionToken(string pathSegment, out string? versionToken)
	{
		versionToken = null;

		if (!pathSegment.StartsWith(DynamicImageConstants.VersionTokenPathSegmentPrefix, StringComparison.Ordinal))
			return false;

		if (pathSegment.Length <= DynamicImageConstants.VersionTokenPathSegmentPrefix.Length)
			return false;

		versionToken = pathSegment[DynamicImageConstants.VersionTokenPathSegmentPrefix.Length..];

		return !string.IsNullOrWhiteSpace(versionToken);
	}

#if NET7_0_OR_GREATER
	[GeneratedRegex("@([0-9]*)x$", RegexOptions.IgnoreCase)]
	private static partial Regex CreateDensityRegex();
#else
	private static Regex CreateDensityRegex() => new("@([0-9]*)x$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
#endif
}
