using System.Globalization;

namespace Umbrella.DataAnnotations.BaseClasses;

/// <summary>
/// Serves as the base class for contingent validation attributes.
/// </summary>
/// <seealso cref="ModelAwareValidationAttribute" />
[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public abstract class ContingentValidationAttribute : ModelAwareValidationAttribute
{
	/// <summary>
	/// Gets the name of the dependent property.
	/// </summary>
	/// <remarks>
	/// This name can match a property with the same name on the model, e.g. FirstName, or can be a path to a nested property,
	/// e.g. Car.Model
	/// </remarks>
	public string DependentProperty { get; private set; }

	/// <summary>
	/// Gets or sets a value indicating whether validation will succeed if either
	/// the value on which this attrubute has been applied is null or the value of
	/// the <see cref="DependentProperty"/> is null.
	/// </summary>
	public bool ReturnTrueOnEitherNull { get; set; }

	/// <summary>
	/// Initializes a new instance of the <see cref="ContingentValidationAttribute"/> class.
	/// </summary>
	/// <param name="dependentProperty">The dependent property name.</param>
	protected ContingentValidationAttribute(string dependentProperty)
	{
		DependentProperty = dependentProperty;
	}

	/// <inheritdoc />
	public override string FormatErrorMessage(string name)
	{
		if (string.IsNullOrEmpty(ErrorMessageResourceName) && string.IsNullOrEmpty(ErrorMessage))
			ErrorMessage = DefaultErrorMessageFormat;

		return string.Format(CultureInfo.CurrentCulture, ErrorMessageString, name, DependentProperty);
	}

	/// <inheritdoc />
	public override string DefaultErrorMessageFormat { get; } = "{0} is invalid due to {1}.";

	/// <summary>
	/// Gets the dependent property value.
	/// </summary>
	/// <param name="container">The container.</param>
	/// <returns>The value of the dependent property from the container.</returns>
	private object? GetDependentPropertyValue(object container)
	{
		var currentType = container.GetType();
		object? value = container;

		// TODO: Could use a cached compiled expression here instead of reflection.
		// Not sure of performance impact on Blazor, but makes sense for server apps.
		// Would need to have a static property to change the configuration between web and client
		// apps. Benchmark first though to see what the best approach is.
		// Also, would need to copy the CreateMemberAccess method from Umbrella.Utilities to avoid
		// creating a dependency on that package. I want to try and keep this package lightweight.

		// ParameterExpression parameter = Expression.Parameter(currentType);
		// var memberAccess = UmbrellaDynamicExpression.CreateMemberAccess(parameter, DependentPropertyName, false);
		// var x = Expression.Lambda<Func<object, object>>(Expression.Convert(memberAccess, typeof(object)), parameter).Compile();

		foreach (string propertyName in DependentProperty.Split('.'))
		{
			var property = currentType?.GetProperty(propertyName);
			value = property?.GetValue(value, null);
			currentType = property?.PropertyType;
		}

		return value;
	}

	/// <inheritdoc />
	protected override IEnumerable<KeyValuePair<string, object>> GetClientValidationParameters()
		=> base.GetClientValidationParameters().Union(new[] { new KeyValuePair<string, object>("DependentProperty", DependentProperty) });

	/// <inheritdoc />
	public override bool IsValid(object? value, object model)
	{
		if (model is null)
			throw new ArgumentNullException(nameof(model));

		return IsValid(value, GetDependentPropertyValue(model), model);
	}

	/// <summary>
	/// If the value of <paramref name="actualDependentPropertyValue" /> is such that it matches the value of the <see cref="DependentProperty"/> value on
	/// the <paramref name="model"/>, validation will take place on the supplied <paramref name="value"/>.
	/// </summary>
	/// <param name="value">The value.</param>
	/// <param name="actualDependentPropertyValue">The comparison value.</param>
	/// <param name="model">The model.</param>
	/// <returns>
	///   <c>true</c> if the specified value is valid; otherwise, <c>false</c>.
	/// </returns>
	public abstract bool IsValid(object? value, object? actualDependentPropertyValue, object model);
}
