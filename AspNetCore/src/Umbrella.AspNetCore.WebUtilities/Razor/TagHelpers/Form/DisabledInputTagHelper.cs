using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Logging;
using Umbrella.WebUtilities.Exceptions;

namespace Umbrella.AspNetCore.WebUtilities.Razor.TagHelpers.Form;

/// <summary>
/// A Tag Helper targeting input and textarea elements that output the 'disabled' property based on the value of the asp-disabled attribute.
/// </summary>
/// <seealso cref="TagHelper" />
[HtmlTargetElement("input", Attributes = DisabledAttributeName)]
[HtmlTargetElement("textarea", Attributes = DisabledAttributeName)]
public class DisabledInputTagHelper : TagHelper
{
	/// <summary>
	/// The disabled attribute name.
	/// </summary>
	protected const string DisabledAttributeName = "asp-disabled";

	private static readonly TagHelperAttribute _disabledAttribute = new("disabled");

	private readonly ILogger<DisabledInputTagHelper> _logger;

	/// <summary>
	/// Gets or sets a value indicating whether the disabled property should be output.
	/// </summary>
	[HtmlAttributeName(DisabledAttributeName)]
	public bool Disabled { get; set; }

	/// <summary>
	/// Initializes a new instance of the <see cref="DisabledInputTagHelper"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	public DisabledInputTagHelper(
		ILogger<DisabledInputTagHelper> logger)
	{
		_logger = logger;
	}

	/// <inheritdoc />
	public override void Process(TagHelperContext context, TagHelperOutput output)
	{
		ArgumentNullException.ThrowIfNull(output);

		try
		{
			if (Disabled)
				output.Attributes.Add(_disabledAttribute);
		}
		catch (Exception exc) when (_logger.WriteError(exc))
		{
			throw new UmbrellaWebException("An error occurred whilst processing the disabled attribute.", exc);
		}
	}
}
