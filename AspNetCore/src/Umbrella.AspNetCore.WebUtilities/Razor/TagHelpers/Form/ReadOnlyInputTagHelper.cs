using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Logging;
using Umbrella.WebUtilities.Exceptions;

namespace Umbrella.AspNetCore.WebUtilities.Razor.TagHelpers.Form;

/// <summary>
/// A Tag Helper targeting input and textarea elements that output the 'readonly' property based on the value of the asp-readonly attribute.
/// </summary>
/// <seealso cref="TagHelper" />
[HtmlTargetElement("input", Attributes = ReadOnlyAttributeName)]
[HtmlTargetElement("textarea", Attributes = ReadOnlyAttributeName)]
public class ReadOnlyInputTagHelper : TagHelper
{
	/// <summary>
	/// The readonly attribute name.
	/// </summary>
	protected const string ReadOnlyAttributeName = "asp-readonly";

	private static readonly TagHelperAttribute _readonlyAttribute = new("readonly");

	private readonly ILogger _logger;

	/// <summary>
	/// Gets or sets a value indicating whether the readonly property should be output.
	/// </summary>
	[HtmlAttributeName(ReadOnlyAttributeName)]
	public bool ReadOnly { get; set; }

	/// <summary>
	/// Initializes a new instance of the <see cref="ReadOnlyInputTagHelper"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	public ReadOnlyInputTagHelper(
		ILogger<ReadOnlyInputTagHelper> logger)
	{
		_logger = logger;
	}

	/// <inheritdoc />
	public override void Process(TagHelperContext context, TagHelperOutput output)
	{
		ArgumentNullException.ThrowIfNull(output);

		try
		{
			if (ReadOnly)
				output.Attributes.Add(_readonlyAttribute);
		}
		catch (Exception exc) when (_logger.WriteError(exc))
		{
			throw new UmbrellaWebException("An error occurred whilst processing the readonly attribute.", exc);
		}
	}
}
