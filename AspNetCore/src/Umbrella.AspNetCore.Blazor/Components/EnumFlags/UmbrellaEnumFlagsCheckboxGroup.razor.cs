using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using CommunityToolkit.Diagnostics;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;
using Umbrella.AspNetCore.Blazor.Components.Checkbox;
using Umbrella.AspNetCore.Blazor.Constants;
using Umbrella.Utilities.Helpers;

namespace Umbrella.AspNetCore.Blazor.Components.EnumFlags;

/// <summary>
/// A component that can be used to render all possible values of an enum that has been marked using the
/// <see cref="FlagsAttribute"/> .
/// </summary>
/// <typeparam name="TEnum">The type of the enum for the options being rendered.</typeparam>
/// <seealso cref="InputBase{TEnum}" />
public partial class UmbrellaEnumFlagsCheckboxGroup<TEnum> : InputBase<TEnum>
	where TEnum : struct, Enum
{
	[Inject]
	private ILogger<UmbrellaEnumFlagsCheckboxGroup<TEnum>> Logger { get; [RequiresUnreferencedCode(TrimConstants.DI)] set; } = null!;

	private UmbrellaEnumFlagsCheckboxGroupItem<TEnum>? ShowAllOptionItem { get; set; }

	/// <summary>
	/// Gets the options that are being rendered.
	/// </summary>
	protected IReadOnlyCollection<UmbrellaEnumFlagsCheckboxGroupItem<TEnum>> Options { get; private set; } = Array.Empty<UmbrellaEnumFlagsCheckboxGroupItem<TEnum>>();

	/// <summary>
	/// Gets or sets a value indicating whether or not there should be an option at the top of the rendered list of
	/// checkboxes that can be used to select or deselect all other options.
	/// </summary>
	[Parameter]
	public bool ShowAllOption { get; set; }

	/// <summary>
	/// Gets or sets the display name of 'All' option.
	/// </summary>
	/// <remarks>
	/// Defaults to <c> All</c>
	/// </remarks>
	[Parameter]
	public string AllOptionDisplayName { get; set; } = "All";

	/// <summary>
	/// Gets or sets an optional callback that can be used to determine how the enum value is displayed as a string in the
	/// UI.
	/// </summary>
	[Parameter]
	public Func<TEnum, string>? OptionDisplayNameSelector { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether or not descriptions should be shown for each option if they exist.
	/// </summary>
	[Parameter]
	public bool ShowDescriptions { get; set; }

	/// <inheritdoc />
	protected override void OnInitialized()
	{
		base.OnInitialized();

		if (!EnumHelper<TEnum>.IsFlags)
			throw new UmbrellaBlazorException("The specified enum type is not marked with the flags attribute.");

		var lstValue = EnumHelper<TEnum>.AllFlagsExceptMinMax;

		var lstOption = new List<UmbrellaEnumFlagsCheckboxGroupItem<TEnum>>(lstValue.Count + 1);

		foreach (var value in lstValue)
		{
			lstOption.Add(new UmbrellaEnumFlagsCheckboxGroupItem<TEnum>(value, OptionDisplayNameSelector?.Invoke(value) ?? value.ToDisplayString(), value.ToDisplayDescriptionString(), CurrentValue.HasFlag(value), false));
		}

		if (ShowAllOption)
		{
			ShowAllOptionItem = new UmbrellaEnumFlagsCheckboxGroupItem<TEnum>(default!, AllOptionDisplayName, null, lstOption.All(x => x.IsSelected), true);

			lstOption.Insert(0, ShowAllOptionItem);
		}

		Options = lstOption;
	}

	/// <summary>
	/// Called by the <see cref="UmbrellaCheckbox"/> component when it's selected state changes.
	/// </summary>
	/// <param name="option">The option being selected or deselected.</param>
	protected void OnOptionSelectionChanged(UmbrellaEnumFlagsCheckboxGroupItem<TEnum> option)
	{
		Guard.IsNotNull(option);

		option.IsSelected = !option.IsSelected;

		if (ShowAllOptionItem is not null && !option.IsAllOption)
		{
			ShowAllOptionItem.IsSelected = Options.Where(x => !x.IsAllOption).All(x => x.IsSelected);
		}
		else if (ShowAllOptionItem is not null)
		{
			foreach (var item in Options)
			{
				if (item.IsAllOption)
					continue;

				item.IsSelected = option.IsSelected;
			}
		}

		// NB: We need to do this casting here to ensure that the flags value can be assigned to the Value property.
		// TODO: When upgrading to .NET 7, we can use GenericMath so that we can support Enums
		// with underlying value types other than integers.
		Value = (TEnum)(object)Options
			.Where(x => !x.IsAllOption && x.IsSelected)
			.Aggregate(0, (x, y) => x | Convert.ToInt32(y.Value, CultureInfo.InvariantCulture));

		if (Logger.IsEnabled(LogLevel.Debug))
			Logger.WriteDebug(new { Value, RawValue = Convert.ToInt32(Value, CultureInfo.InvariantCulture) });

		_ = ValueChanged.InvokeAsync(Value);
	}

	/// <inheritdoc />
	protected override bool TryParseValueFromString(string? value, [MaybeNullWhen(false)] out TEnum result, [NotNullWhen(false)] out string? validationErrorMessage)
		=> throw new NotImplementedException($"This component does not parse string inputs. Bind to the '{nameof(CurrentValue)}' property, not '{nameof(CurrentValueAsString)}'.");
}