


using CommunityToolkit.Diagnostics;
using Umbrella.Utilities.Options.Abstractions;

namespace Umbrella.WebUtilities.Middleware.Options;

/// <summary>
/// Options for implementations of the QueryStringParameterToHttpHeaderMiddleware in the ASP.NET and ASP.NET Core projects.
/// </summary>
/// <seealso cref="IValidatableUmbrellaOptions" />
public class QueryStringParameterToHttpHeaderMiddlewareOptions : IValidatableUmbrellaOptions
{
	/// <summary>
	/// Gets the dictionary of QueryString parameter to HTTP Header mappings.
	/// </summary>
	public IReadOnlyDictionary<string, string> Mappings { get; } = new Dictionary<string, string>();

	/// <summary>
	/// Gets or sets the value transformation function to execute on each QueryString parameter value before copying it
	/// to the corresponding HTTP Header.
	/// </summary>
	public Func<string, string>? ValueTransformer { get; set; }

	/// <inheritdoc />
	public void Validate()
	{
		Guard.IsNotNull(Mappings);
		Guard.HasSizeGreaterThan(Mappings, 0);
	}
}