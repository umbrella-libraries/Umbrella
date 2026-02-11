
using System.Net.Http;
using System.Runtime.InteropServices;
using Umbrella.Utilities.Extensions;

namespace Umbrella.AppFramework.Http.Handlers.Options;

/// <summary>
/// Specifies a path and <see cref="HttpMethod"/> that will be ignored when the <see cref="RequestNotificationHandler"/>
/// is processing a request.
/// </summary>
/// <seealso cref="IEquatable{RequestNotificationHandlerExclusion}" />
[StructLayout(LayoutKind.Auto)]
public readonly struct RequestNotificationHandlerExclusion : IEquatable<RequestNotificationHandlerExclusion>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="RequestNotificationHandlerExclusion"/> struct.
	/// </summary>
	/// <param name="path">The path, e.g. /api/person</param>
	/// <param name="method">The method.</param>
	/// <param name="pathMatchType">The type to use when matching the <see cref="Path"/>.</param>
	public RequestNotificationHandlerExclusion(string path, HttpMethod method, RequestNotificationHandlerPathMatchType pathMatchType = RequestNotificationHandlerPathMatchType.Exact)
	{
		Path = path.TrimToLowerInvariant();

		if (!Path.StartsWith("/", StringComparison.Ordinal))
			Path = "/" + Path;

		Method = method;
		PathMatchType = pathMatchType;
	}

	/// <summary>
	/// Gets the absolute path.
	/// </summary>
	public string Path { get; }

	/// <summary>
	/// Gets the <see cref="HttpMethod"/>.
	/// </summary>
	public HttpMethod Method { get; }

	/// <summary>
	/// Gets the type of the path match.
	/// </summary>
	public RequestNotificationHandlerPathMatchType PathMatchType { get; }

	/// <inheritdoc />
	public override bool Equals(object? obj) => obj is RequestNotificationHandlerExclusion exclusion && Equals(exclusion);

	/// <inheritdoc />
	public bool Equals(RequestNotificationHandlerExclusion other) => Path == other.Path && EqualityComparer<HttpMethod>.Default.Equals(Method, other.Method);

	/// <inheritdoc />
	public override int GetHashCode()
	{
		int hashCode = -1266948330;
		hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(Path);
		hashCode = (hashCode * -1521134295) + EqualityComparer<HttpMethod>.Default.GetHashCode(Method);
		return hashCode;
	}

	/// <summary>
	/// Implements the operator ==.
	/// </summary>
	/// <param name="left">The left.</param>
	/// <param name="right">The right.</param>
	/// <returns>
	/// The result of the operator.
	/// </returns>
	public static bool operator ==(RequestNotificationHandlerExclusion left, RequestNotificationHandlerExclusion right) => left.Equals(right);

	/// <summary>
	/// Implements the operator !=.
	/// </summary>
	/// <param name="left">The left.</param>
	/// <param name="right">The right.</param>
	/// <returns>
	/// The result of the operator.
	/// </returns>
	public static bool operator !=(RequestNotificationHandlerExclusion left, RequestNotificationHandlerExclusion right) => !(left == right);
}