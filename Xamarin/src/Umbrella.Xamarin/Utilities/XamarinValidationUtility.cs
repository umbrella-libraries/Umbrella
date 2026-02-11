


using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using Umbrella.Utilities.DataAnnotations.Abstractions;
using Umbrella.Utilities.Extensions;
using Umbrella.Xamarin.Controls;
using Umbrella.Xamarin.Exceptions;
using Umbrella.Xamarin.Utilities.Abstractions;
using Xamarin.Forms;

namespace Umbrella.Xamarin.Utilities;

/// <summary>
/// A utililty class used to enable model validation of types annotated with <see cref="System.ComponentModel.DataAnnotations"/> attributes
/// and then update the UI to reflect any validation errors.
/// </summary>
public class XamarinValidationUtility : IXamarinValidationUtility
{
	private const string ValidationLabelSuffix = "Error";
	private static readonly BindableProperty _validationAttachedProperty = BindableProperty.Create("ValidationAttached", typeof(string), typeof(InputView));

	private readonly ILogger<XamarinValidationUtility> _logger;
	private readonly IObjectGraphValidator _validator;

	/// <summary>
	/// Initializes a new instance of the <see cref="XamarinValidationUtility"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="validator">The validator.</param>
	public XamarinValidationUtility(
		ILogger<XamarinValidationUtility> logger,
		IObjectGraphValidator validator)
	{
		_logger = logger;
		_validator = validator;
	}

	/// <inheritdoc />
	public (bool isValid, IReadOnlyCollection<ValidationResult> results) ValidateProperty(object model, object? property, string propertyName, Page? page, Func<List<ValidationResult>, IReadOnlyCollection<ValidationResult>>? resultsModifier = null, bool attachInlineValiation = true)
	{
		Guard.IsNotNull(model);
		Guard.IsNotNull(page);

		try
		{
			string[] properties = GetValidatablePropertyNames(model);
			var qualifiedPropertyNames = properties.Where(x => x.EndsWith(propertyName, StringComparison.OrdinalIgnoreCase));
			qualifiedPropertyNames.ForEach(x => HideValidationField(x, page!));

			IReadOnlyCollection<ValidationResult> lstError = new List<ValidationResult>();
			var context = new ValidationContext(model) { MemberName = propertyName };
			bool isValid = Validator.TryValidateProperty(property, context, lstError as List<ValidationResult>);

			if (!isValid)
			{
				if (resultsModifier is not null)
					lstError = resultsModifier([.. lstError]);

				ShowValidationFields(lstError, model, page!, false, resultsModifier, attachInlineValiation);
			}

			return (isValid, lstError);
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { page!.Title, propertyName, attachInlineValiation }))
		{
			throw new UmbrellaXamarinException("There has been a problem validating the specified model.", exc);
		}
	}

	/// <inheritdoc />
	public (bool isValid, IReadOnlyCollection<ValidationResult> results) ValidateModel(object model, Page? page, Func<List<ValidationResult>, IReadOnlyCollection<ValidationResult>>? resultsModifier = null, bool deep = false, bool attachInlineValiation = true)
	{
		Guard.IsNotNull(page, nameof(page));

		try
		{
			HideValidationFields(model, page!);

			IReadOnlyCollection<ValidationResult> lstError;
			var context = new ValidationContext(model);
			bool isValid;

			if (deep)
			{
				(isValid, lstError) = _validator.TryValidateObject(model, context, true);
			}
			else
			{
				lstError = new List<ValidationResult>();
				isValid = Validator.TryValidateObject(model, context, lstError as List<ValidationResult>, true);
			}

			if (!isValid)
			{
				if (resultsModifier is not null)
					lstError = resultsModifier([.. lstError]);

				ShowValidationFields(lstError, model, page!, deep, resultsModifier, attachInlineValiation);
			}

			return (isValid, lstError);
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { page!.Title, deep, attachInlineValiation }))
		{
			throw new UmbrellaXamarinException("There has been a problem validating the specified model.", exc);
		}
	}

	private static void HideValidationField(string propertyName, Page page)
	{
		// TODO: Need to extend this to allow visual indicator controls. The best way might to have a ContentView
		// e.g. with an Entry and then a label. Can have an enum mode property to toggle between 3 states.
		// Need to figure out the best way of composing this control and then updating the state.
		string errorControlName = $"{propertyName.Replace(".", "_")}{ValidationLabelSuffix}";
		var control = page.FindByName<Label>(errorControlName);

		_ = control?.IsVisible = false;
	}

	/// <inheritdoc />
	public void HideValidationFields(object model, Page? page)
	{
		Guard.IsNotNull(page);

		if (model is null)
			return;

		string[] properties = GetValidatablePropertyNames(model);
		properties.ForEach(x => HideValidationField(x, page));
	}

	private void ShowValidationFields(IReadOnlyCollection<ValidationResult> errors, object model, Page page, bool deep, Func<List<ValidationResult>, IReadOnlyCollection<ValidationResult>>? resultsModifier, bool attachInlineValiation)
	{
		if (model is null)
			return;

		// Look for the property on both the reflected type and base type as a fallback.
		Type modelType = model.GetType();
		Type baseType = modelType.BaseType;

		foreach (var error in errors)
		{
			(Label? errorControl, string propertyName) GetErrorControl(Type type)
			{
				if (error is null)
					return default;

				string propertyName = $"{type.Name}_{error.MemberNames.FirstOrDefault()}".Replace(".", "_");

				string errorControlName = $"{propertyName}{ValidationLabelSuffix}";
				return (page.FindByName<Label>(errorControlName), propertyName);
			}

			var (errorControl, propertyName) = GetErrorControl(modelType);

			if (errorControl is null)
				(errorControl, propertyName) = GetErrorControl(baseType);

			if (errorControl is not null)
			{
				errorControl.Text = $"{error.ErrorMessage}";
				errorControl.IsVisible = true;

				if (attachInlineValiation)
				{
					object objValidatedControl = page.FindByName(propertyName);

					if (objValidatedControl is View view && view.GetValue(_validationAttachedProperty) is null)
					{
						view.SetValue(_validationAttachedProperty, true);

						// TODO: This currently doesn't work for groups of items, e.g. toggle buttons. Would need to having a naming convention
						// whereby the GroupName for a toggle button was the propertyName. We could then attach handlers to all items in that group.
						switch (view)
						{
							case CheckBox checkBox:
								checkBox.CheckedChanged += (sender, args) => ValidateModel(model, page, resultsModifier, deep);
								break;
							case RadioButton radioButton:
								radioButton.CheckedChanged += (sender, args) => ValidateModel(model, page, resultsModifier, deep);
								break;
							case InputView inputView:
								inputView.TextChanged += (sender, args) => ValidateModel(model, page, resultsModifier, deep);
								break;
							case Picker picker:
								picker.SelectedIndexChanged += (sender, args) => ValidateModel(model, page, resultsModifier, deep);
								break;
							case DatePicker datePicker:
								datePicker.DateSelected += (sender, args) => ValidateModel(model, page, resultsModifier, deep);
								break;
							case Stepper stepper:
								stepper.ValueChanged += (sender, args) => ValidateModel(model, page, resultsModifier, deep);
								break;
							case Slider slider:
								slider.ValueChanged += (sender, args) => ValidateModel(model, page, resultsModifier, deep);
								break;
							case Switch @switch:
								@switch.Toggled += (sender, args) => ValidateModel(model, page, resultsModifier, deep);
								break;
							case TimePicker timePicker:
								timePicker.PropertyChanged += (sender, args) =>
								{
									// NB: Have to manually set this up because there isn't an specific event.
									if (args.PropertyName == TimePicker.TimeProperty.PropertyName)
										_ = ValidateModel(model, page, resultsModifier, deep);
								};
								break;
							case ToggleButton toggleButton:
								toggleButton.Toggled += (sender, args) => ValidateModel(model, page, resultsModifier, deep);
								break;
							case ToggleImageButton toggleImageButton:
								toggleImageButton.Toggled += (sender, args) => ValidateModel(model, page, resultsModifier, deep);
								break;
							default:
								AttachViewChangedEventHandler(propertyName, view, model, page, deep, resultsModifier);
								break;
						}
					}
				}
			}
		}
	}

	/// <summary>
	/// Used to attach an event handler to the specified <paramref name="view"/> that can be used to call the <see cref="ValidateModel(object, Page?, Func{List{ValidationResult}, IReadOnlyCollection{ValidationResult}}?, bool, bool)"/>
	/// method when there is a change in the view. This is only needed to be overridden when the default logic cannot handle the view.
	/// </summary>
	/// <param name="propertyName">Name of the property.</param>
	/// <param name="view">The view.</param>
	/// <param name="model">The model.</param>
	/// <param name="page">The page.</param>
	/// <param name="deep">if set to <c>true</c> performs deep model validation.</param>
	/// <param name="resultsModifier">The results modifier.</param>
	/// <exception cref="NotSupportedException">The specified view cannot be handled by the Xamarin Validation Utility. Please override the {nameof(AttachViewChangedEventHandler)} method and handle explicitly.</exception>
	protected virtual void AttachViewChangedEventHandler(string propertyName, View view, object model, Page page, bool deep, Func<List<ValidationResult>, IReadOnlyCollection<ValidationResult>>? resultsModifier)
		=> throw new NotSupportedException($"The specified view cannot be handled by the Xamarin Validation Utility. Please override the {nameof(AttachViewChangedEventHandler)} method and handle explicitly.");

	private static string[] GetValidatablePropertyNames(object model)
	{
		var validatableProperties = new List<string>();

		var properties = model.GetType().GetProperties().Where(prop => prop.CanRead
			&& prop.GetCustomAttributes(typeof(ValidationAttribute), true).Any()
			&& prop.GetIndexParameters().Length == 0).ToList();

		// Looking for the property in multiple places here. This allows
		// a base class to be used for multiple models so the property can either be on the base or
		// the reflected type.
		foreach (var propertyInfo in properties)
		{
			string errorControlName = $"{propertyInfo.ReflectedType.Name}.{propertyInfo.Name}";
			string errorControlNameBase = $"{propertyInfo.DeclaringType.Name}.{propertyInfo.Name}";
			validatableProperties.Add(errorControlName);
			validatableProperties.Add(errorControlNameBase);
		}
		
		return validatableProperties.Distinct().ToArray();
	}
}