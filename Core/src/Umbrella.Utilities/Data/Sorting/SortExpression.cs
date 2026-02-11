
using System.Linq.Expressions;
using System.Runtime.InteropServices;
using CommunityToolkit.Diagnostics;
using Umbrella.Utilities.Data.Abstractions;
using Umbrella.Utilities.Extensions;

namespace Umbrella.Utilities.Data.Sorting;

/// <summary>
/// Specifies how items of <typeparamref name="TItem"/> should be sorted.
/// </summary>
/// <typeparam name="TItem">The type of the item being sorted.</typeparam>
[StructLayout(LayoutKind.Auto)]
public readonly record struct SortExpression<TItem> : IDataExpression<TItem>
{
	private readonly Lazy<Func<TItem, object>>? _lazyFunc;
	private readonly Lazy<string>? _lazyMemberPath;

	/// <summary>
	/// Gets the sort direction.
	/// </summary>
	public SortDirection Direction { get; }

	/// <inheritdoc />
	public Expression<Func<TItem, object>>? Expression { get; }

	/// <inheritdoc />
	public string? MemberPath => _lazyMemberPath?.Value;

	/// <summary>
	/// Initializes a new instance of the <see cref="SortExpression{TItem}"/> struct.
	/// </summary>
	/// <param name="expression">The expression.</param>
	/// <param name="direction">The direction.</param>
	public SortExpression(Expression<Func<TItem, object>> expression, SortDirection direction)
	{
		Guard.IsNotNull(expression, nameof(expression));

		Direction = direction;
		Expression = expression;

		_lazyFunc = new Lazy<Func<TItem, object>>(expression.Compile);
		_lazyMemberPath = new Lazy<string>(expression.GetMemberPath);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SortExpression{TItem}"/> struct.
	/// </summary>
	/// <param name="expression">The expression.</param>
	/// <param name="delegate">The delegate.</param>
	/// <param name="memberPath">The member path.</param>
	/// <param name="direction">The direction.</param>
	/// <remarks>This is dynamically invoked by the <see cref="DataExpressionFactory"/>.</remarks>
	public SortExpression(LambdaExpression expression, Lazy<Delegate> @delegate, Lazy<string> memberPath, SortDirection direction)
	{
		Direction = direction;
		Expression = (Expression<Func<TItem, object>>)expression;

		_lazyFunc = new Lazy<Func<TItem, object>>(() => (Func<TItem, object>)@delegate.Value);
		_lazyMemberPath = memberPath;
	}

	/// <inheritdoc />
	public Func<TItem, object>? GetDelegate() => _lazyFunc?.Value;

	/// <inheritdoc />
	public override string ToString() => $"{MemberPath} - {Direction}";

	/// <summary>
	/// Converts to this instance to a <see cref="SortExpressionDescriptor"/> instance.
	/// </summary>
	/// <returns>The <see cref="SortExpressionDescriptor"/> instance.</returns>
	public SortExpressionDescriptor? ToSortExpressionDescriptor()
		=> (SortExpressionDescriptor?)this;

	/// <summary>
	/// Performs an explicit conversion from <see cref="SortExpression{TItem}"/> to <see cref="SortExpressionDescriptor"/>.
	/// </summary>
	/// <param name="sortExpression">The sort expression.</param>
	/// <returns>
	/// The result of the conversion.
	/// </returns>
	public static explicit operator SortExpressionDescriptor?(SortExpression<TItem> sortExpression)
		=> sortExpression != default && sortExpression.MemberPath is not null
			? new SortExpressionDescriptor(sortExpression.MemberPath, sortExpression.Direction)
			: null;
}