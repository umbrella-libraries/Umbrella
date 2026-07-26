using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Logging;
using Umbrella.WebUtilities.Exceptions;

namespace Umbrella.AspNetCore.WebUtilities.Razor.TagHelpers.Bootstrap;

/// <summary>
/// A tag helper used to set Bootstrap specified attributes to enable scroll-spy behaviour.
/// </summary>
/// <seealso cref="TagHelper" />
[HtmlTargetElement("div", Attributes = "bs-scroll-spy")]
[HtmlTargetElement("section", Attributes = "bs-scroll-spy")]
[HtmlTargetElement("body", Attributes = "bs-scroll-spy")]
[HtmlTargetElement("main", Attributes = "bs-scroll-spy")]
public class BootstrapScrollSpyTagHelper : TagHelper
{
	private readonly ILogger<BootstrapScrollSpyTagHelper> _logger;

	/// <summary>
	/// Gets or sets the scroll spy target.
	/// </summary>
	[HtmlAttributeName("bs-scroll-spy")]
	public string? ScrollSpyTarget { get; set; }

	/// <summary>
	/// Gets or sets the scroll spy offset.
	/// </summary>
	[HtmlAttributeName("bs-scroll-spy-offset")]
	public int? ScrollSpyOffset { get; set; }

	/// <summary>
	/// Initializes a new instance of the <see cref="BootstrapScrollSpyTagHelper"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	public BootstrapScrollSpyTagHelper(
		ILogger<BootstrapScrollSpyTagHelper> logger)
	{
		_logger = logger;
	}

	/// <inheritdoc />
	public override void Process(TagHelperContext context, TagHelperOutput output)
	{
		ArgumentNullException.ThrowIfNull(output);

		try
		{
			ScrollSpyTarget = ScrollSpyTarget?.Trim();

			if (!string.IsNullOrWhiteSpace(ScrollSpyTarget))
			{
				output.Attributes.Add("data-spy", "scroll");
				output.Attributes.Add("data-target", ScrollSpyTarget);

				if (ScrollSpyOffset.HasValue && ScrollSpyOffset > 0)
					output.Attributes.Add("data-offset", ScrollSpyOffset);
			}
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { ScrollSpyTarget }))
		{
			throw new UmbrellaWebException("An error occurred whilst processing the attribute.", exc);
		}
	}
}
