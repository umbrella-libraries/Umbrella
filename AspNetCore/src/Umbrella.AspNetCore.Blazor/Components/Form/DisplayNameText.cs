
using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using CommunityToolkit.Diagnostics;
using Microsoft.AspNetCore.Components.Rendering;

namespace Umbrella.AspNetCore.Blazor.Components.Form;

/// <summary>
/// Renders the value of the <see cref="Content"/> property wrapped in a <![CDATA[<span>]]> element. 
/// If no <see cref="Content"/> has been provided, the code will attempt to find a
/// <see cref="DisplayAttribute"/> applied to the member specified using the <see cref="ForTarget"/> property.
/// </summary>
/// <seealso cref="ComponentBase" />
public class DisplayNameText : ComponentBase
{
	private bool _hasRendered;

	/// <summary>
	/// The target property.
	/// </summary>
	[Parameter]
	public Expression<Func<object>> ForTarget { get; set; } = null!;

	/// <summary>
	/// Gets or sets the content to be rendered by this component. If no content has been provided,
	/// the code will attempt to find a
	/// <see cref="DisplayAttribute"/> applied to the member specified using the <see cref="ForTarget"/> property.
	/// </summary>
	[Parameter]
	public RenderFragment? Content { get; set; }

	/// <summary>
	/// Gets or sets the additional attributes. Contains all unmatched attributes.
	/// </summary>
	[Parameter(CaptureUnmatchedValues = true)]
	public IReadOnlyDictionary<string, object> AdditionalAttributes { get; set; } = null!;

	/// <inheritdoc />
	protected override void OnParametersSet()
	{
		Guard.IsNotNull(ForTarget, nameof(ForTarget));

		if (ForTarget.Body is not MemberExpression and not UnaryExpression)
			throw new ArgumentException("A MemberExpression must be provided.", nameof(ForTarget));
	}

	/// <inheritdoc />
	protected override void BuildRenderTree(RenderTreeBuilder builder)
	{
		Guard.IsNotNull(builder);

		base.BuildRenderTree(builder);

		var expressionBody = ForTarget.Body;

		if (expressionBody is UnaryExpression expression && expression.NodeType is ExpressionType.Convert)
		{
			expressionBody = expression.Operand;
		}

		var me = (MemberExpression)expressionBody;

		builder.OpenElement(0, "span");
		builder.AddMultipleAttributes(1, AdditionalAttributes);

		if (Content is null)
		{
			builder.AddContent(2, ForTarget.GetDisplayText());
		}
		else
		{
			Content(builder);
		}

		builder.CloseElement();

		_hasRendered = true;
	}

	/// <inheritdoc />
	protected override bool ShouldRender() => !_hasRendered;
}