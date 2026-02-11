using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text;
using Humanizer;
using Umbrella.DataAnnotations;
using Umbrella.TypeScript.Annotations.Enumerations;
using Umbrella.Utilities.Extensions;

namespace Umbrella.TypeScript.Generators;

/// <summary>
/// A TypeScript generator implementation for generating Knockout classes.
/// </summary>
/// <seealso cref="BaseClassGenerator" />
public class KnockoutClassGenerator : BaseClassGenerator
{
	#region Private Constants
	/// <summary>
	/// This is the same regex used inside the implementation of the EmailAddressAttribute. Found this by checking the Microsoft Reference Source.
	/// This string has been double escaped so it is output in a JavaScript friendly format.
	/// </summary>
	private const string RegexEmail = "new RegExp(\"^((([a-z]|\\\\d|[!#\\\\$%&'\\\\*\\\\+\\\\-\\\\/=\\\\?\\\\^_`{\\\\|}~]|[\\\\u00A0-\\\\uD7FF\\\\uF900-\\\\uFDCF\\\\uFDF0-\\\\uFFEF])+(\\\\.([a-z]|\\\\d|[!#\\\\$%&'\\\\*\\\\+\\\\-\\\\/=\\\\?\\\\^_`{\\\\|}~]|[\\\\u00A0-\\\\uD7FF\\\\uF900-\\\\uFDCF\\\\uFDF0-\\\\uFFEF])+)*)|((\\\\x22)((((\\\\x20|\\\\x09)*(\\\\x0d\\\\x0a))?(\\\\x20|\\\\x09)+)?(([\\\\x01-\\\\x08\\\\x0b\\\\x0c\\\\x0e-\\\\x1f\\\\x7f]|\\\\x21|[\\\\x23-\\\\x5b]|[\\\\x5d-\\\\x7e]|[\\\\u00A0-\\\\uD7FF\\\\uF900-\\\\uFDCF\\\\uFDF0-\\\\uFFEF])|(\\\\\\\\([\\\\x01-\\\\x09\\\\x0b\\\\x0c\\\\x0d-\\\\x7f]|[\\\\u00A0-\\\\uD7FF\\\\uF900-\\\\uFDCF\\\\uFDF0-\\\\uFFEF]))))*(((\\\\x20|\\\\x09)*(\\\\x0d\\\\x0a))?(\\\\x20|\\\\x09)+)?(\\\\x22)))@((([a-z]|\\\\d|[\\\\u00A0-\\\\uD7FF\\\\uF900-\\\\uFDCF\\\\uFDF0-\\\\uFFEF])|(([a-z]|\\\\d|[\\\\u00A0-\\\\uD7FF\\\\uF900-\\\\uFDCF\\\\uFDF0-\\\\uFFEF])([a-z]|\\\\d|-|\\\\.|_|~|[\\\\u00A0-\\\\uD7FF\\\\uF900-\\\\uFDCF\\\\uFDF0-\\\\uFFEF])*([a-z]|\\\\d|[\\\\u00A0-\\\\uD7FF\\\\uF900-\\\\uFDCF\\\\uFDF0-\\\\uFFEF])))\\\\.)+(([a-z]|[\\\\u00A0-\\\\uD7FF\\\\uF900-\\\\uFDCF\\\\uFDF0-\\\\uFFEF])|(([a-z]|[\\\\u00A0-\\\\uD7FF\\\\uF900-\\\\uFDCF\\\\uFDF0-\\\\uFFEF])([a-z]|\\\\d|-|\\\\.|_|~|[\\\\u00A0-\\\\uD7FF\\\\uF900-\\\\uFDCF\\\\uFDF0-\\\\uFFEF])*([a-z]|[\\\\u00A0-\\\\uD7FF\\\\uF900-\\\\uFDCF\\\\uFDF0-\\\\uFFEF])))\\\\.?$\", \"i\")";
	#endregion

	private readonly bool _useDecorators;

	#region Overridden Properties
	/// <inheritdoc />
	public override TypeScriptOutputModelType OutputModelType => TypeScriptOutputModelType.KnockoutClass;

	/// <inheritdoc />
	protected override bool SupportsValidationRules => true;

	/// <inheritdoc />
	protected override TypeScriptOutputModelType InterfaceModelType => TypeScriptOutputModelType.KnockoutInterface;
	#endregion

	/// <summary>
	/// Initializes a new instance of the <see cref="KnockoutClassGenerator"/> class.
	/// </summary>
	/// <param name="useDecorators">if set to <c>true</c>, uses TypeScript decorators.</param>
	public KnockoutClassGenerator(bool useDecorators)
	{
		_useDecorators = useDecorators;
	}

	#region Overridden Methods
	/// <inheritdoc />
	protected override void WriteProperty(PropertyInfo pi, TypeScriptMemberInfo tsInfo, StringBuilder typeBuilder)
	{
		if (pi is null)
			throw new ArgumentNullException(nameof(pi));

		if (tsInfo is null)
			throw new ArgumentNullException(nameof(tsInfo));

		if (typeBuilder is null)
			throw new ArgumentNullException(nameof(typeBuilder));

		if (!string.IsNullOrEmpty(tsInfo.TypeName))
		{
			string strInitialOutputValue = PropertyMode switch
			{
				TypeScriptPropertyMode.Null => "null",
				TypeScriptPropertyMode.Model => tsInfo.InitialOutputValue ?? "null",
				_ => "",
			};

			string strStrictNullCheck = StrictNullChecks && (tsInfo.IsNullable || PropertyMode == TypeScriptPropertyMode.Null) ? " | null" : "";

			var formatString = new StringBuilder();

			if (_useDecorators)
			{
				_ = formatString.AppendLineWithTabIndent("@observable({ expose: true })", 2);

				StringBuilder? coreValidationBuilder = CreateValidationExtendItems(pi, 3);

				if (coreValidationBuilder?.Length > 0)
				{
					_ = formatString.AppendLineWithTabIndent("@extend({", 2);
					_ = formatString.Append(coreValidationBuilder);
					_ = formatString.AppendLineWithTabIndent("})", 2);
				}

				_ = formatString.AppendLineWithTabIndent($"public {tsInfo.Name}: {tsInfo.TypeName}{strStrictNullCheck} = {strInitialOutputValue};", 2);
			}
			else
			{
				_ = formatString.Append($"\t\t{tsInfo.Name}: ");

#pragma warning disable CA1508 // Avoid dead conditional code
				if (tsInfo.TypeName?.EndsWith("[]", StringComparison.Ordinal) is true)
				{
					tsInfo.TypeName = tsInfo.TypeName.TrimEnd('[', ']');
					_ = formatString.Append($"KnockoutObservableArray<{tsInfo.TypeName}{strStrictNullCheck}> = ko.observableArray<{tsInfo.TypeName}{strStrictNullCheck}>({strInitialOutputValue});");
				}
				else
				{
					_ = formatString.Append($"KnockoutObservable<{tsInfo.TypeName}{strStrictNullCheck}> = ko.observable<{tsInfo.TypeName}{strStrictNullCheck}>({strInitialOutputValue});");
				}
#pragma warning restore CA1508 // Avoid dead conditional code
			}

			_ = typeBuilder.AppendLine(formatString.ToString());
		}
	}

	/// <inheritdoc />
	protected override void WriteValidationRules(PropertyInfo propertyInfo, TypeScriptMemberInfo tsInfo, StringBuilder? validationBuilder)
	{
		if (propertyInfo is null)
			throw new ArgumentNullException(nameof(propertyInfo));

		if (tsInfo is null)
			throw new ArgumentNullException(nameof(tsInfo));

		StringBuilder? ctorExtendBuilder = CreateConstructorValidationRules(propertyInfo);

		(string thisVariable, string exposePrefix) = _useDecorators && ctorExtendBuilder?.Length > 0
			? ("(<any>this)", "_")
			: ("this", "");

		var coreBuilder = !_useDecorators ? CreateValidationExtendItems(propertyInfo) : null;

		if (validationBuilder is not null && (coreBuilder?.Length > 0 || ctorExtendBuilder?.Length > 0))
		{
			if (_useDecorators)
			{
				_ = validationBuilder
					.AppendLineWithTabIndent($"{thisVariable}.{exposePrefix}{tsInfo.Name.Camelize()}.extend({{", 3);
			}
			else
			{
				_ = validationBuilder
					.AppendLineWithTabIndent($"{thisVariable}.{exposePrefix}{tsInfo.Name.Camelize()} = {thisVariable}.{exposePrefix}{tsInfo.Name.Camelize()}.extend({{", 3);
			}

			_ = validationBuilder
				.Append(coreBuilder)
				.Append(ctorExtendBuilder)
				.AppendLineWithTabIndent("});", 3)
				.AppendLine();
		}
	}

	private static StringBuilder? CreateValidationExtendItems(PropertyInfo propertyInfo, int indent = 4)
	{
		// Get all types that are either of type ValidationAttribute or derive from it
		// However, specifically exclude instances of type DataTypeAttribute
		var lstValidationAttribute = propertyInfo.GetCustomAttributes<ValidationAttribute>().Where(x => x.GetType() != typeof(DataTypeAttribute)).ToList();

		if (lstValidationAttribute.Count is 0)
			return null;

		var validationBuilder = new StringBuilder();

		for (int i = 0; i < lstValidationAttribute.Count; i++)
		{
			var validationAttribute = lstValidationAttribute[i];

			string message = $"\"{validationAttribute.FormatErrorMessage(propertyInfo.Name)}\"";

			switch (validationAttribute)
			{
				case RequiredAttribute attr:
				case RequiredNonEmptyCollectionAttribute attrCol:
					_ = validationBuilder.AppendLineWithTabIndent($"required: {{ params: true, message: {message} }},", indent);
					break;
				case CompareAttribute attr:
					string otherPropertyName = attr.OtherProperty.Camelize();
					_ = validationBuilder.AppendLineWithTabIndent($"equal: {{ params: this.{otherPropertyName}, message: {message} }},", indent);
					break;
				case EmailAddressAttribute attr:
					_ = validationBuilder.AppendLineWithTabIndent($"pattern: {{ params: {RegexEmail}, message: {message} }},", indent);
					break;
				case MinLengthAttribute attr:
					_ = validationBuilder.AppendLineWithTabIndent($"minLength: {{ params: {attr.Length}, message: {message} }},", indent);
					break;
				case MaxLengthAttribute attr:
					_ = validationBuilder.AppendLineWithTabIndent($"maxLength: {{ params: {attr.Length}, message: {message} }},", indent);
					break;
				case RangeAttribute attr:
					_ = validationBuilder.AppendLineWithTabIndent($"min: {{ params: {attr.Minimum}, message: {message} }},", indent);
					_ = validationBuilder.AppendLineWithTabIndent($"max: {{ params: {attr.Maximum}, message: {message} }},", indent);
					break;
				case RegularExpressionAttribute attr:
					_ = validationBuilder.AppendLineWithTabIndent($"pattern: {{ params: /{attr.Pattern}/, message: {message} }},", indent);
					break;
				case StringLengthAttribute attr:
					if (attr.MinimumLength > 0)
						_ = validationBuilder.AppendLineWithTabIndent($"minLength: {{ params: {attr.MinimumLength}, message: {message} }},", indent);

					_ = validationBuilder.AppendLineWithTabIndent($"maxLength: {{ params: {attr.MaximumLength}, message: {message} }},", indent);
					break;
				case RequiredTrueAttribute attr:
					_ = validationBuilder.AppendLineWithTabIndent($"validation: {{ params: true, validator: (val, otherVal) => val === otherVal, message: {message} }},", indent);
					break;
			}
		}

		return validationBuilder;
	}

	private StringBuilder? CreateConstructorValidationRules(PropertyInfo propertyInfo, int indent = 4)
	{
		string GetDependentPropertyName(string dependentProperty)
		{
			string propertyName = dependentProperty.Contains(".")
				? string.Join("?.", dependentProperty.Split('.').Select(x => x.Camelize()))
				: dependentProperty.Camelize();

			// If we are not using decorators we need to access the value using a method call.
			if (!_useDecorators)
				propertyName += "()";

			return propertyName;
		}

		static string GetOperatorTranslation(EqualityOperator @operator) => @operator switch
		{
			EqualityOperator.EqualTo => "===",
			EqualityOperator.GreaterThan => ">",
			EqualityOperator.GreaterThanOrEqualTo => ">=",
			EqualityOperator.LessThan => "<",
			EqualityOperator.LessThanOrEqualTo => "<=",
			EqualityOperator.NotEqualTo => "!==",
			EqualityOperator.MaxPercentageOf => "<=",
			EqualityOperator.NotRegExMatch => throw new NotImplementedException(),
			EqualityOperator.RegExMatch => throw new NotImplementedException(),
			_ => throw new NotSupportedException()
		};

		static string GetDependentValueTranslation(object dependentValue) => dependentValue switch
		{
			bool b when b => "true",
			bool b when !b => "false",
			string s => $"'{s}'",
			Enum e => $"{e.GetType().FullName}.{e}",
			_ => dependentValue.ToString()
		};

		// Get all types that are either of type ValidationAttribute or derive from it
		// However, specifically exclude instances of type DataTypeAttribute
		var lstValidationAttributeGroup = propertyInfo
			.GetCustomAttributes<ValidationAttribute>()
			.Where(x => x.GetType() != typeof(DataTypeAttribute))
			.GroupBy(x => x.GetType())
			.ToArray();

		if (lstValidationAttributeGroup.Length is 0)
			return null;

		var validationBuilder = new StringBuilder();
		var lstCustomValidationRule = new List<string>();

		foreach (var group in lstValidationAttributeGroup)
		{
			var lstValidationAttribute = group.ToArray();

			// Arbitrarily use the first message from the group
			string message = $"\"{lstValidationAttribute[0].FormatErrorMessage(propertyInfo.Name)}\"";

			var sbRule = new StringBuilder();

			if (typeof(RequiredIfAttribute).IsAssignableFrom(group.Key))
			{
				for (int i = 0; i < lstValidationAttribute.Length; i++)
				{
					var attr = (RequiredIfAttribute)lstValidationAttribute[i];

					if (i > 0)
						_ = sbRule.Append(" || ");

					string @operator = GetOperatorTranslation(attr.Operator);
					string otherValue = GetDependentValueTranslation(attr.ComparisonValue);
					string dependentPropertyName = GetDependentPropertyName(attr.DependentProperty);

					_ = sbRule.Append($"(this.{dependentPropertyName} {@operator} {otherValue})");
				}

				_ = validationBuilder.AppendLineWithTabIndent($"required: {{ onlyIf: () => {sbRule}, message: {message} }},", indent);
			}
			else if (typeof(RequiredIfEmptyAttribute).IsAssignableFrom(group.Key))
			{
				for (int i = 0; i < lstValidationAttribute.Length; i++)
				{
					var attr = (RequiredIfEmptyAttribute)lstValidationAttribute[i];

					if (i > 0)
						_ = sbRule.Append(" || ");

					string dependentPropertyName = GetDependentPropertyName(attr.DependentProperty);

					_ = sbRule.Append($"(this.{dependentPropertyName} === undefined || this.{dependentPropertyName} === null || (typeof this.{dependentPropertyName} === \"number\" && isNaN(this.{dependentPropertyName}!)) || (typeof this.{dependentPropertyName} === \"string\" && (this.{dependentPropertyName}! as any).trim().length === 0))");
				}

				_ = validationBuilder.AppendLineWithTabIndent($"required: {{ onlyIf: () => {sbRule}, message: {message} }},", indent);
			}
			else if (typeof(RequiredIfNotEmptyAttribute).IsAssignableFrom(group.Key))
			{
				for (int i = 0; i < lstValidationAttribute.Length; i++)
				{
					var attr = (RequiredIfNotEmptyAttribute)lstValidationAttribute[i];

					if (i > 0)
						_ = sbRule.Append(" || ");

					string dependentPropertyName = GetDependentPropertyName(attr.DependentProperty);

					_ = sbRule.Append($"(this.{dependentPropertyName} !== undefined && this.{dependentPropertyName} !== null && ((typeof this.{dependentPropertyName} === \"number\" && !isNaN(this.{dependentPropertyName}!)) || (typeof this.{dependentPropertyName} === \"string\" && (this.{dependentPropertyName}! as any).trim().length > 0)))");
				}

				_ = validationBuilder.AppendLineWithTabIndent($"required: {{ onlyIf: () => {sbRule}, message: {message} }},", indent);
			}
			else if (typeof(IsAttribute).IsAssignableFrom(group.Key))
			{
				Type? type = null;
				string? tsType = null;

				for (int i = 0; i < lstValidationAttribute.Length; i++)
				{
					var attr = (IsAttribute)lstValidationAttribute[i];

					if (i == 0)
					{
						type = Nullable.GetUnderlyingType(propertyInfo.PropertyType) ?? propertyInfo.PropertyType;

						tsType = type switch
						{
							var _ when type == typeof(string) => "string | null",
							var _ when type == typeof(bool) => "boolean | null",
							var _ when type.IsPrimitive => "number | null",
							_ => "any"
						};
					}
					else
					{
						_ = sbRule.Append(" || ");
					}

					string @operator = GetOperatorTranslation(attr.Operator);
					string dependentPropertyName = GetDependentPropertyName(attr.DependentProperty);

					if (attr is MaxPercentageOfAttribute maxPercentageOfAttribute)
					{
						_ = sbRule.Append($"(value === undefined || value === null || this.{dependentPropertyName} === undefined || this.{dependentPropertyName} === null || value {@operator} this.{dependentPropertyName}! * {maxPercentageOfAttribute.MaxPercentage})");
					}
					else
					{
						_ = sbRule.Append($"(value === undefined || value === null || this.{dependentPropertyName} === undefined || this.{dependentPropertyName} === null || value {@operator} this.{dependentPropertyName}!)");
					}
				}

				lstCustomValidationRule.Add($"{{ validator: (value: {tsType}) => {sbRule}, message: {message} }},");
			}
		}

		if (lstCustomValidationRule.Count > 0)
		{
			var sbCustomValidationBuilder = new StringBuilder();

			foreach (string rule in lstCustomValidationRule)
			{
				_ = sbCustomValidationBuilder.AppendLineWithTabIndent(rule, indent);
			}

			_ = validationBuilder.AppendLineWithTabIndent("validation: [", indent);
			_ = validationBuilder.AppendLineWithTabIndent(sbCustomValidationBuilder.ToString(), indent);
			_ = validationBuilder.AppendLineWithTabIndent("]", indent);
		}

		return validationBuilder;
	}

	/// <inheritdoc />
	protected override void WriteEnd(Type modelType, StringBuilder typeBuilder, StringBuilder? validationBuilder)
	{
		if (typeBuilder is null)
			throw new ArgumentNullException(nameof(typeBuilder));

		// Only write the validation rules if some validation rules have been generated
		// and we are not using decorators
		if (validationBuilder?.Length > 0)
		{
			_ = typeBuilder.AppendLine();

			//Write out the constructor
			_ = typeBuilder.AppendLineWithTabIndent("constructor()", 2)
				.AppendLineWithTabIndent("{", 2)
				.Append(validationBuilder.ToString())
				.AppendLineWithTabIndent("}", 2)
				.AppendLine();
		}

		base.WriteEnd(modelType, typeBuilder, validationBuilder);
	}
	#endregion
}