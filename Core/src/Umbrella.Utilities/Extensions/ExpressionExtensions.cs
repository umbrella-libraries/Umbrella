// Copyright (c) Zinofi Digital Ltd. All Rights Reserved.
// Licensed under the MIT License.

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using CommunityToolkit.Diagnostics;
using Humanizer;
using Umbrella.Utilities.Data.Attributes;
using Umbrella.Utilities.Exceptions;
using Umbrella.Utilities.Expressions;

namespace Umbrella.Utilities.Extensions;

/// <summary>
/// Extension methods for the <see cref="Expression"/> type.
/// </summary>
public static class ExpressionExtensions
{
	/// <summary>
	/// Gets the name of the member specified in the supplied expression.
	/// </summary>
	/// <param name="expression">The expression.</param>
	/// <param name="throwException">
	/// if set to <see langword="true"/> , an exception will be thrown when the member name cannot be determined instead of
	/// returning null.
	/// </param>
	/// <returns>The member name.</returns>
	/// <exception cref="Exception">
	/// The body of the expression must be either a {nameof(MemberExpression)} or a {nameof(UnaryExpression)}.
	/// </exception>
	public static string? GetMemberName(this LambdaExpression expression, bool throwException = true)
	{
		Guard.IsNotNull(expression);

		MemberExpression? memberExpression = expression.Body switch
		{
			UnaryExpression x => x.Operand as MemberExpression,
			_ => expression.Body as MemberExpression
		};

		return memberExpression is null && throwException
			? throw new UmbrellaException($"The body of the expression must be either a {nameof(MemberExpression)} or a {nameof(UnaryExpression)}.")
			: (memberExpression?.Member?.Name);
	}

	/// <summary>
	/// Gets the full path of the member specified in the supplied expression, e.g. it will transform Parent -> Child ->
	/// Name into Parent.Child.Name.
	/// </summary>
	/// <param name="lambdaExpression">The expression.</param>
	/// <returns>The member path.</returns>
	/// <exception cref="Exception">
	/// The body of the expression must be either a {nameof(MemberExpression)} or a {nameof(UnaryExpression)}.
	/// </exception>
	public static string GetMemberPath(this LambdaExpression lambdaExpression)
	{
		Guard.IsNotNull(lambdaExpression, nameof(lambdaExpression));

		var parts = new List<string>();

		// Inner Method
		void ParsePath(Expression? expression)
		{
			if (expression is null)
				return;

			while (expression.NodeType is ExpressionType.Convert or ExpressionType.ConvertChecked)
			{
				expression = ((UnaryExpression)expression).Operand;
			}

			if (expression is MemberExpression memberExpression)
			{
				parts.Add(memberExpression.Member.Name);
				ParsePath(memberExpression.Expression);
			}
			else if (expression is MethodCallExpression methodCallExpression && methodCallExpression.Method.Name == "Select" && methodCallExpression.Arguments.Count == 2)
			{
				if (methodCallExpression.Arguments[1] is LambdaExpression subExpression && subExpression.Body is MemberExpression subMemberExpression)
				{
					parts.Add(subMemberExpression.Member.Name);
					ParsePath(subMemberExpression.Expression);
				}

				if (methodCallExpression.Arguments[0] is MemberExpression innerMemberExpression)
				{
					parts.Add(innerMemberExpression.Member.Name);
					ParsePath(innerMemberExpression.Expression);
				}
			}
		}

		ParsePath(lambdaExpression.Body);

		parts.Reverse();

		return string.Join(".", parts);
	}

	/// <summary>
	/// Combines two given predicates using a conditional AND operation.
	/// </summary>
	/// <typeparam name="TSource">The type of the predicate's parameter.</typeparam>
	/// <param name="left">The first predicate expression to combine.</param>
	/// <param name="right">The second predicate expression to combine.</param>
	/// <returns>A single combined predicate expression.</returns>
	public static Expression<Func<TSource, bool>> CombineAnd<TSource>(this Expression<Func<TSource, bool>> left, Expression<Func<TSource, bool>> right)
	{
		Guard.IsNotNull(left, nameof(left));
		Guard.IsNotNull(right, nameof(right));

		var l = left.Parameters[0];
		var r = right.Parameters[0];

		var binder = new ParameterBinder(l, r);

		return Expression.Lambda<Func<TSource, bool>>(
			Expression.AndAlso(binder.Visit(left.Body), right.Body), r);
	}

	/// <summary>
	/// Combines two given predicates using a conditional OR operation.
	/// </summary>
	/// <typeparam name="TSource">The type of the predicate's parameter.</typeparam>
	/// <param name="left">The first predicate expression to combine.</param>
	/// <param name="right">The second predicate expression to combine.</param>
	/// <returns>A single combined predicate expression.</returns>
	public static Expression<Func<TSource, bool>> CombineOr<TSource>(this Expression<Func<TSource, bool>> left, Expression<Func<TSource, bool>> right)
	{
		Guard.IsNotNull(left);
		Guard.IsNotNull(right);

		var l = left.Parameters[0];
		var r = right.Parameters[0];

		var binder = new ParameterBinder(l, r);

		return Expression.Lambda<Func<TSource, bool>>(
			Expression.OrElse(binder.Visit(left.Body), right.Body), r);
	}

	/// <summary>
	/// Gets the display text for the given expression by trying the following in order: <list type="bullet"> <item> Use a
	/// <see cref="DisplayAttribute"/> .</item> <item> Use a <see cref="DisplayNameAttribute"/> .</item> <item> Use
	/// Humanizer to convert the member name to a friendly string using <see cref="LetterCasing.Title"/> .</item> </list>
	/// </summary>
	/// <param name="expression">The expression.</param>
	/// <returns>The display name according to the specified rules.</returns>
	public static string? GetDisplayText(this LambdaExpression expression)
	{
		Guard.IsNotNull(expression);

		MemberExpression? memberExpression = GetMemberExpression(expression);

		return memberExpression?.Member.GetDisplayText();
	}

	/// <summary>
	/// Gets the short display text for the given expression by trying to read the <see cref="DisplayAttribute.ShortName"/>
	/// property of a <see cref="DisplayAttribute"/> that has been applied to the member specified using the given
	/// expression.
	/// </summary>
	/// <param name="expression">The expression.</param>
	/// <returns>The short display name if it has a value; otherwise <see langword="null"/>.</returns>
	public static string? GetShortNameDisplayText(this LambdaExpression expression)
	{
		Guard.IsNotNull(expression);

		MemberExpression? memberExpression = GetMemberExpression(expression);

		return memberExpression?.Member.GetShortDisplayText();
	}

	/// <summary>
	/// Gets the display description for the given expression by trying the following in order: <list type="bullet"> <item>
	/// Use a <see cref="DisplayAttribute"/> .</item> <item> Use a <see cref="DescriptionAttribute"/> .</item> </list>
	/// </summary>
	/// <param name="expression">The expression.</param>
	/// <returns>The display description if it has a value; otherwise <see langword="null"/>.</returns>
	public static string? GetDisplayDescriptionText(this LambdaExpression expression)
	{
		Guard.IsNotNull(expression);

		MemberExpression? memberExpression = GetMemberExpression(expression);
		
		return memberExpression?.Member.GetDisplayDescriptionText();
	}

	/// <summary>
	/// Gets the member path for the given expression by trying to read the <see cref="UmbrellaMemberPathAttribute.Value"/>
	/// property of a <see cref="UmbrellaMemberPathAttribute"/> that has been applied to the member specified using the
	/// given expression.
	/// </summary>
	/// <param name="expression">The expression.</param>
	/// <returns>The member path if it has a value; otherwise <see langword="null"/>.</returns>
	public static string? GetUmbrellaMemberPath(this LambdaExpression expression)
	{
		MemberExpression? memberExpression = GetMemberExpression(expression);

		return memberExpression?.Member.GetUmbrellaMemberPath();
	}

	private static MemberExpression? GetMemberExpression(this LambdaExpression expression) => expression.Body switch
	{
		UnaryExpression x => x.Operand as MemberExpression,
		_ => expression.Body as MemberExpression
	};
}