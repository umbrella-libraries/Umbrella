


using System.Diagnostics.CodeAnalysis;
using Xamarin.Forms;

namespace Umbrella.Xamarin.Behaviors;

/// <summary>
/// A custom <see cref="CollectionView"/> behavior that mitigates issues that sometime arise where
/// the its height does not correctly fill the height of its parent layout. This manifests itself most frequently
/// on iOS where the control only fills half the screen and gets cut off.
/// </summary>
/// <seealso cref="Behavior{CollectionView}" />
public class CollectionViewHeightBehavior : Behavior<CollectionView>
{
	/// <summary>
	/// The bindable property for the <see cref="ParentLayout"/> property.
	/// </summary>
	[SuppressMessage("Usage", "CA2211:Non-constant fields should not be visible", Justification = "Required by Xamarin")]
	public static BindableProperty ParentLayoutProperty = BindableProperty.Create(nameof(ParentLayout), typeof(Layout), typeof(CollectionViewHeightBehavior), null, BindingMode.OneTime);

	private bool _hasHeightChanged;

	/// <summary>
	/// Gets or sets the parent layout on which the height of the <see cref="CollectionView"/> should be based.
	/// </summary>
	public Layout? ParentLayout
	{
		get => (Layout?)GetValue(ParentLayoutProperty);
		set => SetValue(ParentLayoutProperty, value);
	}

	/// <summary>
	/// Gets the collection view object that this behavior targets.
	/// </summary>
	public CollectionView? CollectionViewObject { get; private set; }

	/// <inheritdoc />
	protected override void OnAttachedTo(CollectionView bindable)
	{
		base.OnAttachedTo(bindable);
		CollectionViewObject = bindable;
		bindable.BindingContextChanged += Bindable_BindingContextChanged;
		bindable.SizeChanged += Bindable_SizeChanged;
	}

	[SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "This is an event handler.")]
	private async void Bindable_SizeChanged(object sender, EventArgs e)
	{
		if (CollectionViewObject is null)
			return;

		if (ParentLayout is null)
			return;

		if (!_hasHeightChanged)
		{
			double collectionHeight = CollectionViewObject.Height;

			if (collectionHeight > 0)
			{
				await Task.Delay(50);

				double newHeight = (collectionHeight / 2) + 20;

				CollectionViewObject.HeightRequest = Math.Max(newHeight, ParentLayout.Height);

				_hasHeightChanged = true;
			}
		}
	}

	private void Bindable_BindingContextChanged(object sender, EventArgs e) => OnBindingContextChanged();

	/// <inheritdoc />
	protected override void OnBindingContextChanged()
	{
		base.OnBindingContextChanged();

		if (CollectionViewObject is null)
			return;

		BindingContext = CollectionViewObject.BindingContext;
	}

	/// <inheritdoc />
	protected override void OnDetachingFrom(CollectionView bindable)
	{
		base.OnDetachingFrom(bindable);

		bindable.BindingContextChanged -= Bindable_BindingContextChanged;
		bindable.SizeChanged -= Bindable_SizeChanged;
	}
}