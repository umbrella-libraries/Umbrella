using System.Linq.Expressions;

namespace Umbrella.DataAccess.Abstractions.Test;

public class IncludeMapTest
{
	[Fact]
	public void Valid()
	{
		var expressions = new Expression<Func<Type, object?>>[]
		{
			x => x.Name,
			x => x.Assembly,
			x => x.Assembly.Location,
			x => x.Assembly.ImageRuntimeVersion.Length,
			x => x.Assembly.ExportedTypes.Select(x => x.FullName),
			x => x.Assembly.ExportedTypes.Select(x => x.FullName!.Length),
			x => x.GenericTypeArguments.Select(x => x.FullName),
			x => x.GenericTypeArguments.Select(x => x.Assembly.FullName!.Length),
			// TODO: Extends to allow nested Select/SelectMany calls. Needed though??
			//x => x.GenericTypeArguments.Select(x => x.GenericTypeArguments.Select(x => x.FullName.Length)),
			//x => x.GenericTypeArguments.SelectMany(x => x.GenericTypeArguments.Select(x => x.FullName.Length))
		};

		var map = new IncludeMap<Type>(expressions);

		// Includes
		Assert.Equal(expressions.Length, map.Includes.Count);
		Assert.True(expressions.SequenceEqual(map.Includes));

		// Property Paths
		string[] propertyPaths =
		[
			"Name",
			"Assembly",
			"Assembly.Location",
			"Assembly.ImageRuntimeVersion.Length",
			"Assembly.ExportedTypes.FullName",
			"Assembly.ExportedTypes.FullName.Length",
			"GenericTypeArguments.FullName",
			"GenericTypeArguments.Assembly.FullName.Length",
			//"GenericTypeArguments.GenericTypeArguments.FullName.Length",
			//"GenericTypeArguments.GenericTypeArguments.FullName.Length"
		];

		Assert.Equal(propertyPaths.Length, map.PropertyPaths.Count);
		Assert.True(propertyPaths.SequenceEqual(map.PropertyPaths));

		// String
		Assert.Equal(string.Join(", ", propertyPaths), map.ToString());
	}

	[Fact]
	public void Duplicates_Invalid()
	{
		var expressions = new Expression<Func<Type, object?>>[]
		{
			x => x.Name,
			x => x.Assembly,
			x => x.Assembly.Location,
			x => x.Assembly.ImageRuntimeVersion.Length,
			x => x.Assembly.ExportedTypes.Select(x => x.FullName),
			x => x.Assembly.ExportedTypes.Select(x => x.FullName!.Length),
			x => x.GenericTypeArguments.Select(x => x.FullName),
			x => x.GenericTypeArguments.Select(x => x.Assembly.FullName!.Length),
			x => x.GenericTypeArguments.Select(x => x.Assembly.FullName!.Length)
		};

		var map = new IncludeMap<Type>(expressions);

		var validExpressions = expressions.SkipLast(1).ToArray();

		// Includes
		Assert.Equal(validExpressions.Length, map.Includes.Count);
		Assert.True(validExpressions.SequenceEqual(map.Includes));

		// Property Paths
		string[] propertyPaths =
		[
			"Name",
			"Assembly",
			"Assembly.Location",
			"Assembly.ImageRuntimeVersion.Length",
			"Assembly.ExportedTypes.FullName",
			"Assembly.ExportedTypes.FullName.Length",
			"GenericTypeArguments.FullName",
			"GenericTypeArguments.Assembly.FullName.Length"
		];

		Assert.Equal(propertyPaths.Length, map.PropertyPaths.Count);
		Assert.True(propertyPaths.SequenceEqual(map.PropertyPaths));

		// String
		Assert.Equal(string.Join(", ", propertyPaths), map.ToString());
	}
}