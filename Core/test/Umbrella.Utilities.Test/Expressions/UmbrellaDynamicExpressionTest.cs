namespace Umbrella.Utilities.Test.Expressions;

public static class UmbrellaDynamicExpressionTest
{
	private sealed class TestType
	{
		public int? NullableIntProperty { get; set; }
		public int IntProperty { get; set; }
		public TypeCode? NullableEnumProperty { get; set; }
		public TypeCode EnumProperty { get; set; }
	}

	//[Fact]
	//public void CreateConstant_NullableIntPropertyMemberAccess()
	//{
	//	var target = Expression.Parameter(typeof(TestType));

	//	var memberAccess = UmbrellaDynamicExpression.CreateMemberAccess(target, nameof(TestType.NullableIntProperty));

	//	//var constant = UmbrellaDynamicExpression.CreateConstant(target, memberAccess!, "1", null);

	//	//var memberAccess1 = UmbrellaDynamicExpression.CreateMemberAccess(target, nameof(TestType.NullableEnumProperty));
	//	//var constant1 = UmbrellaDynamicExpression.CreateConstant(target, memberAccess1!, nameof(TypeCode.Boolean), null);

	//	//int i = 0;
	//}
}