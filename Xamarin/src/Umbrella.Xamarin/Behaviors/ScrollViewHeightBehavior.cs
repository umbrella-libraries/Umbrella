using System.Diagnostics.CodeAnalysis;
using Xamarin.Forms;

namespace Umbrella.Xamarin.Behaviors;

/// <summary>
/// A custom <see cref="ScrollView"/> behavior that mitigates issues that sometime arise where
/// the its height does not correctly fill the height of its parent layout. This manifests itself most frequently
/// on iOS where the control only fills half the screen and gets cut off.
/// </summary>
/// <seealso cref="Behavior{ScrollView}" />
[SuppressMessage("Usage", "CA2211:Non-constant fields should not be visible", Justification = "Required by Xamarin's conventions.")]
public class ScrollViewHeightBehavior : Behavior<ScrollView>
{
	/// <summary>
	/// The bindable property for the <see cref="ParentLayout"/> property.
	/// </summary>
	public static BindableProperty ParentLayoutProperty = BindableProperty.Create(nameof(ParentLayout), typeof(Layout), typeof(ScrollViewHeightBehavior), null, BindingMode.OneTime);

	private bool _hasHeightChanged;

	/// <summary>
	/// Gets or sets the parent layout on which the height of the <see cref="ScrollView"/> should be based.
	/// </summary>
	public Layout? ParentLayout
	{
		get => (Layout?)GetValue(ParentLayoutProperty);
		set => SetValue(ParentLayoutProperty, value);
	}

	/// <summary>
	/// Gets the scroll view object that this behavior targets.
	/// </summary>
	public ScrollView? ScrollViewObject { get; private set; }

	/// <inheritdoc />
	protected override void OnAttachedTo(ScrollView bindable)
	{
		base.OnAttachedTo(bindable);
		ScrollViewObject = bindable;
		bindable.BindingContextChanged += Bindable_BindingContextChanged;
		bindable.SizeChanged += Bindable_SizeChanged;
	}

	/// <inheritdoc />
	protected override void OnBindingContextChanged()
	{
		base.OnBindingContextChanged();

		if (ScrollViewObject is null)
			return;

		BindingContext = ScrollViewObject.BindingContext;
	}

	/// <inheritdoc />
	protected override void OnDetachingFrom(ScrollView bindable)
	{
		base.OnDetachingFrom(bindable);

		bindable.BindingContextChanged -= Bindable_BindingContextChanged;
		bindable.SizeChanged -= Bindable_SizeChanged;
	}

	[SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "This is an event handler.")]
	private async void Bindable_SizeChanged(object sender, EventArgs e)
	{
		if (ScrollViewObject is null)
			return;

		if (ParentLayout is null)
			return;

		if (!_hasHeightChanged)
		{
			double collectionHeight = ScrollViewObject.Height;

			if (collectionHeight > 0)
			{
				await Task.Delay(50);

				double newHeight = (collectionHeight / 2) + 20;

				ScrollViewObject.HeightRequest = Math.Max(newHeight, ParentLayout.Height);

				_hasHeightChanged = true;
			}
		}
	}

	private void Bindable_BindingContextChanged(object sender, EventArgs e) => OnBindingContextChanged();
}