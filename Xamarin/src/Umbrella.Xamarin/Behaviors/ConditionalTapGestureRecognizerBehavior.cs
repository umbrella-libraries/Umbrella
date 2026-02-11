using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Xamarin.Forms;

namespace Umbrella.Xamarin.Behaviors;

/// <summary>
/// A behaviour that can be applied to any <see cref="View"/> to conditionally add or remove a <see cref="TapGestureRecognizer"/> at runtime.
/// </summary>
/// <seealso cref="Behavior{View}" />
[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2211:Non-constant fields should not be visible", Justification = "Required by Xamarin's conventions.")]
public class ConditionalTapGestureRecognizerBehavior : Behavior<View>
{
	/// <summary>
	/// The bindable property for the <see cref="IsEnabled"/> property.
	/// </summary>
	public static BindableProperty IsEnabledProperty = BindableProperty.Create(nameof(IsEnabled), typeof(bool), typeof(ConditionalTapGestureRecognizerBehavior), false);

	/// <summary>
	/// The bindable property for the <see cref="Command"/> property.
	/// </summary>
	public static BindableProperty CommandProperty = BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(ConditionalTapGestureRecognizerBehavior), null);

	/// <summary>
	/// The bindable property for the <see cref="CommandParameter"/> property.
	/// </summary>
	public static BindableProperty CommandParameterProperty = BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(ConditionalTapGestureRecognizerBehavior), null);

	private readonly ILogger _logger;
	private View? _view;
	private TapGestureRecognizer? _gestureRecognizer;

	/// <summary>
	/// Initializes a new instance of the <see cref="ConditionalTapGestureRecognizerBehavior"/> class.
	/// </summary>
	public ConditionalTapGestureRecognizerBehavior()
	{
		_logger = UmbrellaXamarinServices.GetService<ILogger<ConditionalTapGestureRecognizerBehavior>>();
	}

	/// <summary>
	/// Gets or sets a value that determines if this behavior is enabled.
	/// </summary>
	public bool IsEnabled
	{
		get => (bool)GetValue(IsEnabledProperty);
		set => SetValue(IsEnabledProperty, value);
	}

	/// <summary>
	/// Gets or sets the command.
	/// </summary>
	public ICommand? Command
	{
		get => (ICommand?)GetValue(CommandProperty);
		set => SetValue(CommandProperty, value);
	}

	/// <summary>
	/// Gets or sets the parameter passed to the <see cref="Command" />.
	/// </summary>
	public object? CommandParameter
	{
		get => GetValue(CommandParameterProperty);
		set => SetValue(CommandParameterProperty, value);
	}

	/// <inheritdoc />
	protected override void OnAttachedTo(View bindable)
	{
		base.OnAttachedTo(bindable);

		_view = bindable;

		if (IsEnabled && Command is not null)
		{
			AddGestureRecognizer(bindable);
		}
	}

	/// <inheritdoc />
	protected override void OnPropertyChanged([CallerMemberName] string? propertyName = null)
	{
		base.OnPropertyChanged(propertyName);

		if (_view is null)
			return;

		if (propertyName == nameof(IsEnabled))
		{
			if (IsEnabled && Command is not null)
			{
				AddGestureRecognizer(_view);
			}
			else
			{
				RemoveGestureRecognizer(_view);
			}
		}
		else if (propertyName is (nameof(Command)) or (nameof(CommandParameter)))
		{
			if (IsEnabled && Command is not null)
			{
				if (_gestureRecognizer is null)
				{
					AddGestureRecognizer(_view);
				}
				else
				{
					UpdateGestureRecognizer();
				}
			}
			else
			{
				RemoveGestureRecognizer(_view);
			}
		}
	}

	/// <inheritdoc />
	protected override void OnDetachingFrom(View bindable)
	{
		base.OnDetachingFrom(bindable);

		_view = null;
		RemoveGestureRecognizer(bindable);
	}

	private void AddGestureRecognizer(View bindable)
	{
		_gestureRecognizer = new TapGestureRecognizer
		{
			Command = Command,
			CommandParameter = CommandParameter
		};

		bindable.GestureRecognizers.Add(_gestureRecognizer);
	}

	private void UpdateGestureRecognizer()
	{
		if (_gestureRecognizer is not null)
		{
			_gestureRecognizer.Command = Command;
			_gestureRecognizer.CommandParameter = CommandParameter;
		}
	}

	private void RemoveGestureRecognizer(View bindable)
	{
		try
		{
			if (_gestureRecognizer is not null)
			{
				_ = bindable.GestureRecognizers.Remove(_gestureRecognizer);
				_gestureRecognizer = null;
			}
		}
		catch (Exception exc) when (_logger.WriteError(exc, message: "There has been a problem removing the gesture recognizer."))
		{
			_gestureRecognizer = null;
		}
	}
}