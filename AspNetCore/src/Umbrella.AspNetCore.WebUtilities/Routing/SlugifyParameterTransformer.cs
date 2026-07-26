using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Routing;

namespace Umbrella.AspNetCore.WebUtilities.Routing;

/// <summary>
/// A route transformer which slugifies route parameters and returns them in lowercase, e.g. transforms "ManageAccount" to "manage-account"
/// </summary>
/// <seealso cref="IOutboundParameterTransformer" />
public partial class SlugifyParameterTransformer : IOutboundParameterTransformer
{
	private static readonly Regex _urlTransformer = CreateUrlTransformer();

	/// <inheritdoc />
	public string? TransformOutbound(object? value) => value is null ? null : _urlTransformer.Replace(value.ToString() ?? string.Empty, "$1-$2").ToLowerInvariant();

	[GeneratedRegex("([a-z])([A-Z])", RegexOptions.CultureInvariant)]
	private static partial Regex CreateUrlTransformer();
}
