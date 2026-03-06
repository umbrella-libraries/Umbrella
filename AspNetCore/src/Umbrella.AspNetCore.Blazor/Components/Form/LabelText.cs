
using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using CommunityToolkit.Diagnostics;
using Microsoft.AspNetCore.Components.Rendering;

namespace Umbrella.AspNetCore.Blazor.Components.Form;

/// <summary>
/// A component that renders a label element associated with a specific model property. The label text can be specified
/// using the <see cref="Content"/> parameter. If it is not specified, the <see cref="DisplayAttribute"/> is used if it
/// exists; otherwise the property name is used.
/// </summary>
/// <seealso cref="ComponentBase" />
public class LabelText : ComponentBase
{
	private bool _hasRendered;

	/// <summary>
	/// Gets or sets the target model property.
	/// </summary>
	[Parameter]
	public Expression<Func<object>> ForTarget { get; set; } = null!;

	/// <summary>
	/// Gets or sets the custom content of the label.
	/// </summary>
	[Parameter]
	public RenderFragment? Content { get; set; }

	/// <summary>
	/// Gets or sets the additional attributes.
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

		if (expressionBody is UnaryExpression expression && expression.NodeType == ExpressionType.Convert)
		{
			expressionBody = expression.Operand;
		}

		var me = (MemberExpression)expressionBody;

		builder.OpenElement(0, "label");
		builder.AddMultipleAttributes(1, AdditionalAttributes);
		builder.AddAttribute(2, "for", ForTarget.GetMemberPath());

		if (Content is null)
		{
			string? displayText = ForTarget.GetDisplayText()?.NormalizeNewLines().Replace("\r\n", "<br />", StringComparison.Ordinal);

			if (!string.IsNullOrEmpty(displayText))
				builder.AddMarkupContent(3, displayText);
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