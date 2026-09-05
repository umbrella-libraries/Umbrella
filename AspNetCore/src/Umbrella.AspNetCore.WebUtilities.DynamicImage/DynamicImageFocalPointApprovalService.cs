using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Umbrella.DynamicImage.Abstractions;
using Umbrella.FileSystem.Abstractions;
using Umbrella.WebUtilities.DynamicImage.Middleware.Options;

namespace Umbrella.AspNetCore.WebUtilities.DynamicImage;

/// <summary>Issues and verifies deterministic image-bound focal approvals.</summary>
public sealed class DynamicImageFocalPointApprovalService : IDynamicImageDescriptorFactory
{
	private readonly Dictionary<string, byte[]> _keys = new(StringComparer.Ordinal);
	private readonly string _activeKeyId;
	private readonly string _route;
	private readonly string? _stripPrefix;
	private readonly ILogger<DynamicImageFocalPointApprovalService> _logger;

	/// <summary>Creates the service and validates configured signing keys.</summary>
	/// <param name="options">The server signing options.</param>
	/// <param name="middlewareOptions">The middleware route options.</param>
	/// <param name="logger">The logger.</param>
	public DynamicImageFocalPointApprovalService(DynamicImageFocalPointSigningOptions options, DynamicImageMiddlewareOptions middlewareOptions, ILogger<DynamicImageFocalPointApprovalService> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(middlewareOptions);
		ArgumentNullException.ThrowIfNull(logger);
		_logger = logger;
		_activeKeyId = options.ActiveKeyId;
		_route = middlewareOptions.DynamicImagePathPrefix.Trim('/').ToLowerInvariant();
		_stripPrefix = options.StripPrefix;
		foreach (var item in options.Keys)
		{
			if (item.Key.Length is 0 or > 64 || item.Key.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_'))
				throw new ArgumentException("Signing key identifiers must contain 1–64 ASCII letters, digits, underscores or hyphens.", nameof(options));
			byte[] key = Convert.FromBase64String(item.Value);
			if (key.Length < 32)
				throw new ArgumentException("Focal approval signing keys require at least 32 random bytes.", nameof(options));
			_keys.Add(item.Key, key);
		}

		if ((_keys.Count > 0 || _activeKeyId.Length > 0) && !_keys.ContainsKey(_activeKeyId))
			throw new ArgumentException("The active focal approval key must exist in Keys.", nameof(options));
	}

	/// <inheritdoc />
	public DynamicImageDescriptor? Create(UmbrellaVersionedUrl? image, double? focalPointX = null, double? focalPointY = null)
	{
		if (focalPointX.HasValue != focalPointY.HasValue)
			throw new ArgumentException("Supply both focal coordinates or neither.");
		DynamicImageFocalPoint? point = focalPointX.HasValue ? new(focalPointX.Value, focalPointY!.Value) : null;
		if (!image.HasValue)
			return null;
		try
		{
			string? approval = null;
			if (point.HasValue)
			{
				if (!_keys.TryGetValue(_activeKeyId, out byte[]? key))
					throw new InvalidOperationException("Configure focalPointSigningOptionsBuilder with an ActiveKeyId and a persistent signing key before issuing focal approvals.");
				string path = image.Value.Url;
				if (!string.IsNullOrEmpty(_stripPrefix) && path.StartsWith(_stripPrefix, StringComparison.OrdinalIgnoreCase))
					path = path[_stripPrefix.Length..];
				byte[] payload = CreatePayload(path, image.Value.VersionToken, point.Value);
				approval = "1." + _activeKeyId + "." + WebEncoders.Base64UrlEncode(HMACSHA256.HashData(key, payload));
			}

			return new DynamicImageDescriptor { Url = image.Value.Url, VersionToken = image.Value.VersionToken, FocalPoint = point, FocalPointApproval = approval };
		}
		catch (Exception exc) when (_logger.WriteError(exc))
		{
			throw;
		}
	}

	/// <summary>Checks approval against the parsed request. Invalid input is rejected without disclosing keys.</summary>
	/// <param name="options">The parsed request.</param>
	/// <returns>Whether the approval is valid.</returns>
	public bool Verify(DynamicImageOptions options)
	{
		if (!options.FocalPointX.HasValue || !options.FocalPointY.HasValue || options.FocalPointApproval is not { Length: <= 160 } token)
			return false;
		try
		{
			string[] parts = token.Split('.');
			if (parts.Length is not 3 || parts[0] is not "1" || !_keys.TryGetValue(parts[1], out byte[]? key))
				return false;
			byte[] mac = WebEncoders.Base64UrlDecode(parts[2]);
			byte[] expected = HMACSHA256.HashData(key, CreatePayload(options.SourcePath, options.VersionToken, new(options.FocalPointX.Value, options.FocalPointY.Value)));
			return mac.Length is 32 && string.Equals(WebEncoders.Base64UrlEncode(mac), parts[2], StringComparison.Ordinal) && CryptographicOperations.FixedTimeEquals(expected, mac);
		}
		catch (ArgumentException)
		{
			return false;
		}
		catch (FormatException)
		{
			return false;
		}
	}

	private byte[] CreatePayload(string path, string? version, DynamicImageFocalPoint point)
	{
		if (string.IsNullOrWhiteSpace(version))
			throw new ArgumentException("A focal approval requires a file version token.", nameof(version));
		path = path.Trim();
		if (path.StartsWith("~/", StringComparison.Ordinal))
			path = path[1..];
		if (!path.StartsWith("/", StringComparison.Ordinal))
			path = "/" + path;
		if (path.Any(c => char.IsControl(c) || c is '\\' or '%' or '?' or '#' or ':') || path.Split('/').Skip(1).Any(s => s is "" or "." or ".."))
			throw new ArgumentException("Focal image paths must be unambiguous local paths without query strings or encoded separators.", nameof(path));
		using var stream = new MemoryStream();
		using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
		{
			writer.Write("Umbrella.DynamicImage.FocalApproval.v1");
			writer.Write(_route);
			// DynamicImageUtility resolves source paths in lowercase.
			writer.Write(path.ToLowerInvariant());
			writer.Write(version.Trim().ToLowerInvariant());
			writer.Write(point.X.ToString("G4", CultureInfo.InvariantCulture));
			writer.Write(point.Y.ToString("G4", CultureInfo.InvariantCulture));
		}

		return stream.ToArray();
	}
}
