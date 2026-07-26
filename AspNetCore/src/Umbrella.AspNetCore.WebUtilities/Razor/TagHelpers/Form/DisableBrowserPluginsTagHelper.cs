using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Logging;
using Umbrella.WebUtilities.Exceptions;

namespace Umbrella.AspNetCore.WebUtilities.Razor.TagHelpers.Form;

/// <summary>
/// A tag helper targeting input and textarea elements that output attributes to disable some default behaviour and other browser plugins as follows:
/// <list type="bullet">
/// <item>autocomplete=off</item>
/// <item>spellcheck=false</item>
/// <item>data-lpignore=true</item>
/// </list>
/// </summary>
/// <seealso cref="TagHelper" />
[HtmlTargetElement("input", Attributes = DisablePluginsAttributeName)]
[HtmlTargetElement("textarea", Attributes = DisablePluginsAttributeName)]
public class DisableBrowserPluginsTagHelper : TagHelper
{
	/// <summary>
	/// The disable plugins attribute name.
	/// </summary>
	protected const string DisablePluginsAttributeName = "asp-disable-plugins";

	private readonly ILogger _logger;

	/// <summary>
	/// Gets or sets a value indicating whether the attributes should be applied.
	/// </summary>
	[HtmlAttributeName(DisablePluginsAttributeName)]
	public bool DisablePlugins { get; set; }

	/// <summary>
	/// Initializes a new instance of the <see cref="DisableBrowserPluginsTagHelper"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	public DisableBrowserPluginsTagHelper(
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
			if (DisablePlugins)
			{
				output.Attributes.Add("autocomplete", "off");
				output.Attributes.Add("spellcheck", "false");
				output.Attributes.Add("data-lpignore", "true");
			}
		}
		catch (Exception exc) when (_logger.WriteError(exc))
		{
			throw new UmbrellaWebException("An error occurred whilst processing the attribute.", exc);
		}
	}
}
