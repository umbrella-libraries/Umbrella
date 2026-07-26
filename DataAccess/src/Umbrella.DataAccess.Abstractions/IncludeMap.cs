using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using Umbrella.Utilities.Extensions;

namespace Umbrella.DataAccess.Abstractions;

/// <summary>
/// A map of property expressions used to load related data of an entity, e.g. load the orders for a customer, as part of the same database query.
/// </summary>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
public class IncludeMap<TEntity>
{
	private sealed class IncludeMapExpressionEqualityComparer : EqualityComparer<Expression<Func<TEntity, object?>>>
	{
		public override bool Equals(Expression<Func<TEntity, object?>>? x, Expression<Func<TEntity, object?>>? y)
		{
			// If both are null, or both are same instance, return true.
			if (ReferenceEquals(x, y))
				return true;

			// If one is null, but not both, return false.
			if (x is null || y is null)
				return false;

			// If the types are different, return false.
			if (x.NodeType != y.NodeType || x.Type != y.Type)
				return false;

			return x.GetMemberPath() == y.GetMemberPath();
		}

#if NET6_0_OR_GREATER
		public override int GetHashCode([DisallowNull] Expression<Func<TEntity, object?>> obj) => obj.GetMemberPath().GetHashCode(StringComparison.InvariantCulture);
#else
		public override int GetHashCode([DisallowNull] Expression<Func<TEntity, object?>> obj) => obj.GetMemberPath().GetHashCode();
#endif
	}

	private readonly static IncludeMapExpressionEqualityComparer _includeMapExpressionEqualityComparer = new();

	/// <summary>
	/// Gets the includes as expressions.
	/// </summary>
	public IReadOnlyCollection<Expression<Func<TEntity, object?>>> Includes { get; }

	/// <summary>
	/// Gets the includes as string paths, e.g. Parent -> Child -> Name will be Parent.Child.Name.
	/// </summary>
	public IReadOnlyCollection<string> PropertyPaths { get; }

	private IncludeMap()
	{
		Includes = new HashSet<Expression<Func<TEntity, object?>>>();
		PropertyPaths = new HashSet<string>();
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="IncludeMap{TEntity}"/> class.
	/// </summary>
	/// <param name="paths">The property paths.</param>
	public IncludeMap(params Expression<Func<TEntity, object?>>[] paths)
	{
		var includes = new HashSet<Expression<Func<TEntity, object?>>>(_includeMapExpressionEqualityComparer);
		var propertyPaths = new HashSet<string>(StringComparer.InvariantCulture);

		Includes = includes;
		PropertyPaths = propertyPaths;

		if (paths is null)
			return;

		foreach (var path in paths)
		{
			_ = includes.Add(path);

			string propertyPath = path.GetMemberPath();

			if (!string.IsNullOrEmpty(propertyPath))
				_ = propertyPaths.Add(propertyPath);
		}
	}

	/// <summary>
	/// Combines the specified includes with the <see cref="Includes"/> property of the current instance.
	/// </summary>
	/// <param name="includes">The includes.</param>
	/// <returns>A new combined <see cref="IncludeMap{TEntity}"/> instance.</returns>
	public IncludeMap<TEntity> Combine(params IncludeMap<TEntity>[] includes)
	{
		var x = includes.Select(x => x.Includes).SelectMany(x => x).Concat(Includes).ToArray();

		return new IncludeMap<TEntity>(x);
	}

	/// <inheritdoc />
	public override string ToString() => string.Join(", ", PropertyPaths);
}
