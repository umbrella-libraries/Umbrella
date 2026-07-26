using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.TagHelpers;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Logging;
using Umbrella.WebUtilities.Exceptions;

namespace Umbrella.AspNetCore.WebUtilities.Razor.TagHelpers.Navigation;

/// <summary>
/// A tag helper used with anchor tags that applied a class to the tag when the current request url matches the href parameter of the tag.
/// </summary>
/// <seealso cref="AnchorTagHelper" />
[OutputElementHint("a")]
[HtmlTargetElement("a", Attributes = ActiveClassAttributeName)]
public class ActiveTagHelper : AnchorTagHelper
{
	/// <summary>
	/// The active class attribute name.
	/// </summary>
	protected const string ActiveClassAttributeName = "um-active-class";

	private readonly ILogger _log;

	/// <summary>
	/// Gets or sets the active class name.
	/// </summary>
	[HtmlAttributeName(ActiveClassAttributeName)]
	public string ActiveClass { get; set; } = null!;

	/// <summary>
	/// Gets or sets the match type.
	/// </summary>
	[HtmlAttributeName("um-active-match-type")]
	public ActiveTagMatchType MatchType { get; set; }

	/// <summary>
	/// Initializes a new instance of the <see cref="ActiveTagHelper" /> class.
	/// </summary>
	/// <param name="generator">The generator.</param>
	/// <param name="logger">The logger.</param>
	public ActiveTagHelper(
				IHtmlGenerator generator,
				ILogger<ActiveTagHelper> logger)
				: base(generator)
	{
		_log = logger;
	}

	/// <inheritdoc />
	public override void Process(TagHelperContext context, TagHelperOutput output)
	{
		ArgumentNullException.ThrowIfNull(output);

		try
		{
			// NB: This runs after the built-in tag helper which populates the correct href for us.
			// We can just use this and do string based comparisons to figure out if the current tag should be marked
			// with the Active class.
			if (!(output.Attributes["href"]?.Value is HtmlString hrefHtmlString))
				return;

			string? href = hrefHtmlString.Value;

			if (!string.IsNullOrWhiteSpace(href))
			{
				var request = ViewContext.HttpContext.Request;

				// For virtual applications, when on the root page of the site, the Path value is always empty.
				// For non-virtual applications, the Path is /. We need to ensure consistent behaviour here.
				string currentUrl = request.PathBase.Add(request.Path).ToString().TrimToUpperInvariant().TrimEnd('/');
				string targetUrl = href.TrimToUpperInvariant().TrimEnd('/');

				bool isActive = MatchType switch
				{
					ActiveTagMatchType.Exact => currentUrl == targetUrl,
					ActiveTagMatchType.Hierarchical => currentUrl.StartsWith(targetUrl, StringComparison.Ordinal),
					_ => false
				};

				if (isActive)
					output.AddClass(ActiveClass, HtmlEncoder.Default);
			}
			else
			{
				_ = _log.WriteWarning(state: new { Area, Action, Controller, Page, PageHandler }, message: "The href attribute was empty meaning this tag helper did not run.");
			}
		}
		catch (Exception exc) when (_log.WriteError(exc, new { Area, Action, Controller, Page, PageHandler }))
		{
			throw new UmbrellaWebException("An error has occurred whilst processing this tag.", exc);
		}
	}
}
