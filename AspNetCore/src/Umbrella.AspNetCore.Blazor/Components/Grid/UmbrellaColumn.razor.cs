using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Umbrella.AspNetCore.Blazor.Constants;
using Umbrella.AspNetCore.Shared.Extensions;
using Umbrella.Utilities.Data.Filtering;
using Umbrella.Utilities.Data.Sorting;

namespace Umbrella.AspNetCore.Blazor.Components.Grid;

/// <summary>
/// A column component for use with the <see cref="UmbrellaGrid{TItem}"/> component.
/// </summary>
public class UmbrellaActionsColumn<TItem> : UmbrellaColumn<TItem, object>
	where TItem : notnull
{
	/// <inheritdoc/>
	protected override bool IsActions => true;
}

/// <summary>
/// A column component for use with the <see cref="UmbrellaGrid{TItem}"/> component.
/// </summary>
public partial class UmbrellaColumn<TItem, TValue>
	where TItem : notnull
{
	private Expression<Func<TItem, TValue?>>? _property;
	private Func<TItem, TValue?>? _propertyDelegate;

	[Inject]
	private IServiceProvider ServiceProvider { get; [RequiresUnreferencedCode(TrimConstants.DI)] set; } = null!;

	[Inject]
	private ILogger<UmbrellaColumn<TItem, TValue>> Logger { get; [RequiresUnreferencedCode(TrimConstants.DI)] set; } = null!;

	/// <summary>
	/// Gets or sets the <see cref="IUmbrellaGrid{TItem}"/> instance that contains this column.
	/// </summary>
	[CascadingParameter]
	protected IUmbrellaGrid<TItem> UmbrellaGridInstance { get; set; } = null!;

	[CascadingParameter(Name = nameof(ScanMode))]
	private bool ScanMode { get; set; }

	[NotNull]
	[CascadingParameter]
	private TItem? Value { get; set; }

	private string? PropertyStringValue
	{
		get
		{
			if (Value is null || _propertyDelegate is null)
				return null;

			object? objValue = _propertyDelegate.Invoke(Value);

			if (objValue is null)
				return null;

			return objValue.GetType().IsEnum
				? ((Enum)objValue).ToDisplayString()
				: objValue.ToString();
		}
	}

	/// <summary>
	/// Gets or sets the property selector for this column.
	/// </summary>
	[Parameter]
	public Expression<Func<TItem, TValue?>>? Property
	{
		get => _property;
		set
		{
			_property = value;
			_propertyDelegate = value?.Compile();
		}
	}

	/// <summary>
	/// Gets or sets the property path override used as the <see cref="FilterExpressionDescriptor.MemberPath"/> property
	/// value when creating filters.
	/// </summary>
	/// <remarks>
	/// If this value is <see langword="null"/> , the <see cref="MemberPathOverride"/> will be used.
	/// </remarks>
	[Parameter]
	public string? FilterMemberPathOverride { get; set; }

	/// <summary>
	/// Gets or sets the property path override used as the value <see cref="SortExpressionDescriptor.MemberPath"/>
	/// property value when creating sorters.
	/// </summary>
	/// <remarks>
	/// If this value is <see langword="null"/> , the <see cref="MemberPathOverride"/> will be used.
	/// </remarks>
	[Parameter]
	public string? SorterMemberPathOverride { get; set; }

	/// <summary>
	/// Gets or sets the property path override used as the <see cref="FilterExpressionDescriptor.MemberPath"/> and
	/// <see cref="SortExpressionDescriptor.MemberPath"/> property values when creating filters and sorters.
	/// </summary>
	/// <remarks>
	/// If this value is <see langword="null"/> , the <see cref="Property"/> will be used.
	/// </remarks>
	[Parameter]
	public string? MemberPathOverride { get; set; }

	/// <summary>
	/// Gets or sets the text for the column heading.
	/// </summary>
	/// <remarks>
	/// If not specified, i.e. it is <see langword="null"/> , the code will check for the presence of a
	/// <see cref="DisplayAttribute"/> and use it's value. As a last resort, the member name will be used. To hide the
	/// heading, set this property to an empty string.
	/// </remarks>
	[Parameter]
	public string? Heading { get; set; }

	/// <summary>
	/// Gets the column short heading display text to be applied to the column heading only.
	/// </summary>
	/// <remarks>
	/// If not specified, <see cref="Heading"/> is used. The <see cref="Heading"/> property will always be used for the
	/// filter field name and will also be shown as a tooltip for the column heading where this property has a value.
	/// </remarks>
	[Parameter]
	public string? ShortHeading { get; set; }

	/// <summary>
	/// Gets or sets the percentage width.
	/// </summary>
	[Parameter]
	public int? PercentageWidth { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether this column is sortable.
	/// </summary>
	[Parameter]
	public bool Sortable { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether this column is filterable.
	/// </summary>
	[Parameter]
	public bool Filterable { get; set; }

	/// <summary>
	/// Gets or sets the type of the filter control.
	/// </summary>
	[Parameter]
	public UmbrellaColumnFilterType FilterControlType { get; set; }

	/// <summary>
	/// Gets or sets the type of the filter match. Defaults to <see cref="FilterType.Contains"/> .
	/// </summary>
	[Parameter]
	public FilterType FilterMatchType { get; set; } = FilterType.Contains;

	/// <summary>
	/// Gets or sets the filter options that appear in the drop down when <see cref="FilterControlType"/> is set to
	/// <see cref="UmbrellaColumnFilterType.Options"/> .
	/// </summary>
	[Parameter]
	public IReadOnlyCollection<object>? FilterOptions { get; set; }

	/// <summary>
	/// Gets or sets the type of the filter options.
	/// </summary>
	[Parameter]
	public UmbrellaColumnFilterOptionsType? FilterOptionsType { get; set; }

	/// <summary>
	/// Gets or sets the filter option display name selector used to convert a value in the <see cref="FilterOptions"/>
	/// collection to a friendly display name.
	/// </summary>
	/// <remarks>
	/// An example of where this might be required is when populating the <see cref="FilterOptions"/> collection with a
	/// list of enums. This selector can be used to convert enum names to friendlier formatted display names.
	/// </remarks>
	[Parameter]
	public Func<object, string>? FilterOptionDisplayNameSelector { get; set; }

	/// <summary>
	/// Gets or sets a value that specifies whether or not this column contains actions, e.g. buttons, links, that can be
	/// used to perform actions on the data item that this column is associated with.
	/// </summary>
	protected virtual bool IsActions { get; }

	/// <summary>
	/// Gets or sets the child content rendered by this column. If no child content has been specified, the
	/// <see cref="Value"/> , in conjunction with the <see cref="Property"/> selector is used to render the content.
	/// </summary>
	[Parameter]
	public RenderFragment? ChildContent { get; set; }

	/// <summary>
	/// Gets or sets the uncaptured attributes that have been specified on this component. This dictionary is automatically
	/// populated by Blazor.
	/// </summary>
	[Parameter(CaptureUnmatchedValues = true)]
	public IReadOnlyDictionary<string, object> AdditionalAttributes { get; set; } = null!;

	/// <summary>
	/// Gets or sets the comma-delimited list of roles for which this column should be displayed.
	/// </summary>
	/// <remarks>
	/// Used in conjunction with the <see cref="Policy"/> property.
	/// </remarks>
	[Parameter]
	public string? Roles { get; set; }

	/// <summary>
	/// Gets or sets the authorization policy for which this column should be displayed.
	/// </summary>
	/// <remarks>
	/// Used in conjunction with the <see cref="Roles"/> property.
	/// </remarks>
	[Parameter]
	public string? Policy { get; set; }

	/// <summary>
	/// Gets or sets a value which determines how this column will be rendered.
	/// </summary>
	/// <remarks>
	/// Defaults to <see cref="UmbrellaColumnDisplayMode.Full"/> .
	/// </remarks>
	[Parameter]
	public UmbrellaColumnDisplayMode DisplayMode { get; set; }

	/// <summary>
	/// Gets or sets a function that determines the display mode for the umbrella column.
	/// </summary>
	/// <remarks>
	/// The function should return an instance of UmbrellaColumnDisplayMode based on the current context or conditions.
	/// This allows for dynamic adjustment of the display mode based on user interactions or other criteria.
	/// </remarks>
	[Parameter]
	public Func<UmbrellaColumnDisplayMode>? DisplayModeSelector { get; set; }

	/// <summary>
	/// Gets or sets the nullable enum option.
	/// </summary>
	/// <remarks>
	/// If a value is provided, an option will be shown when the <see cref="FilterOptionsType"/> is set to
	/// <see cref="UmbrellaColumnFilterOptionsType.Enum"/> which will show a new option after the <c> Any</c> option with
	/// an explcit value of <see langword="null" /> with the value specified for this property value displayed as the text
	/// in the dropdown for the option.
	/// </remarks>
	[Parameter]
	public string? NullableEnumOption { get; set; }

	/// <summary>
	/// Gets or sets the addon button delegate which will be invoked when the add-on button is clicked when the
	/// <see cref="FilterControlType"/> is set to <see cref="UmbrellaColumnFilterType.TextAddOnButton"/> .
	/// </summary>
	/// <remarks>
	/// The delegate must accept a string parameter which will be the value of the current filter and return a string,
	/// which is the new filter value to set the text box's content to, wrapped in a <see cref="ValueTask"/> .
	/// </remarks>
	[Parameter]
	public Func<string?, ValueTask<string?>>? OnAddOnButtonClickedAsync { get; set; }

	/// <summary>
	/// Gets or sets the add on button CSS class.
	/// </summary>
	/// <remarks>Defaults to <c>btn btn-secondary</c>.</remarks>
	[Parameter]
	public string? AddOnButtonCssClass { get; set; } = "btn btn-secondary";

	/// <summary>
	/// Gets or sets the add on button text.
	/// </summary>
	/// <remarks>Defaults to <see langword="null"/>.</remarks>
	[Parameter]
	public string? AddOnButtonText { get; set; }

	/// <summary>
	/// Gets or sets the add on button icon CSS class.
	/// </summary>
	/// <remarks>
	/// Defaults to <c> fas list-radio</c>. If no icon is required, set this property value to an empty string or null.
	/// </remarks>
	[Parameter]
	public string? AddOnButtonIconCssClass { get; set; } = "fas fa-list-ul";

	/// <summary>
	/// Gets or sets the AutoComplete search method.
	/// </summary>
	[Parameter]
	public Func<string?, Task<IEnumerable<string>>>? AutoCompleteSearchMethod { get; set; }

	/// <summary>
	/// Gets or sets the AutoComplete debounce in milliseconds.
	/// </summary>
	/// <remarks>Defaults to <c>300ms</c></remarks>
	[Parameter]
	public int AutoCompleteDebounce { get; set; } = 300;

	/// <summary>
	/// Gets or sets the AutoComplete maximum suggestions that will be displayed to the user.
	/// </summary>
	/// <remarks>Defaults to <c>10</c></remarks>
	[Parameter]
	public int AutoCompleteMaximumSuggestions { get; set; } = 10;

	/// <summary>
	/// Gets or sets the minimum characters that need to be provided before the <see cref="AutoCompleteSearchMethod"/>
	/// delegate is invoked.
	/// </summary>
	/// <remarks>Defaults to <c>3</c></remarks>
	[Parameter]
	public int AutoCompleteMinimumLength { get; set; } = 3;

	/// <summary>
	/// Gets a value indicating whether this is the first column in the grid.
	/// </summary>
	public bool IsFirstColumn => UmbrellaGridInstance.FirstColumnPropertyName?.Equals(Property?.GetMemberName(), StringComparison.Ordinal) is true;

	/// <inheritdoc />
	protected override async Task OnInitializedAsync()
	{
		ClaimsPrincipal? claimsPrincipal = null;

		AuthenticationStateProvider? authenticationStateProvider = ServiceProvider.GetService<AuthenticationStateProvider>();

		if (authenticationStateProvider is not null)
		{
			var authState = await authenticationStateProvider.GetAuthenticationStateAsync();

			claimsPrincipal = authState.User;
		}

		claimsPrincipal ??= new ClaimsPrincipal(new ClaimsIdentity());

		// Manually resolve the service here from the provider as the service may not have been registered in the DI container
		// if the component is being used in a project that doesn't require authorization.
		IAuthorizationService? authorizationService = ServiceProvider.GetService<IAuthorizationService>();

		if (authorizationService is null && (!string.IsNullOrWhiteSpace(Roles) || !string.IsNullOrWhiteSpace(Policy)))
			throw new InvalidOperationException("The IAuthorizationService service must be registered in the DI container to use the Roles or Policy properties.");

		if (authorizationService is not null && !await authorizationService.AuthorizeRolesAndPolicyAsync(claimsPrincipal, Roles, Policy))
			DisplayMode = UmbrellaColumnDisplayMode.None;

		if (Logger.IsEnabled(LogLevel.Debug))
			Logger.WriteDebug(new { ScanMode, DisplayMode, PropertyName = Property?.GetMemberName(), Value = Value?.ToString() });

		if (ScanMode)
		{
			if (GetDisplayMode() is not UmbrellaColumnDisplayMode.None)
			{
				if (Logger.IsEnabled(LogLevel.Debug))
					Logger.WriteDebug(message: "Creating column definition.");

				string? memberPathAttributeValue = Property?.GetUmbrellaMemberPath();

				var definition = new UmbrellaColumnDefinition<TItem, TValue>(
					Heading,
					ShortHeading,
					PercentageWidth,
					Sortable,
					Filterable,
					FilterOptions,
					FilterOptionDisplayNameSelector,
					AdditionalAttributes,
					FilterControlType,
					FilterMatchType,
					FilterOptionsType,
					Property,
					FilterMemberPathOverride ?? MemberPathOverride ?? memberPathAttributeValue,
					SorterMemberPathOverride ?? MemberPathOverride ?? memberPathAttributeValue,
					DisplayMode,
					DisplayModeSelector,
					NullableEnumOption,
					OnAddOnButtonClickedAsync,
					AddOnButtonCssClass,
					AddOnButtonText,
					AddOnButtonIconCssClass,
					AutoCompleteDebounce,
					AutoCompleteMaximumSuggestions,
					AutoCompleteMinimumLength,
					AutoCompleteSearchMethod);

				_ = UmbrellaGridInstance.AddColumnDefinition(definition);
			}
		}
	}

	protected UmbrellaColumnDisplayMode GetDisplayMode()
	{
		if (DisplayModeSelector is not null)
			return DisplayModeSelector();

		return DisplayMode;
	}
}