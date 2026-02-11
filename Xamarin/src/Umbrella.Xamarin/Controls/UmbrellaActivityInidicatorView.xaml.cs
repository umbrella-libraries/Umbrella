


using Umbrella.AppFramework.Services.Abstractions;
using Umbrella.AppFramework.Services.Enumerations;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace Umbrella.Xamarin.Controls;

/// <summary>
/// A custom activity indicator that uses the <see cref="ILoadingScreenService.OnStateChanged"/> event internally to control
/// when it is displayed.
/// </summary>
/// <seealso cref="ContentView" />
[XamlCompilation(XamlCompilationOptions.Compile)]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2211:Non-constant fields should not be visible", Justification = "Required by Xamarin's conventions.")]
public partial class UmbrellaActivityInidicatorView : ContentView
{
	/// <summary>
	/// The is loading property
	/// </summary>
	public static BindableProperty IsLoadingProperty = BindableProperty.Create(nameof(IsLoading), typeof(bool), typeof(UmbrellaActivityInidicatorView));

	/// <summary>
	/// The loading background color property
	/// </summary>
	public static BindableProperty LoadingBackgroundColorProperty = BindableProperty.Create(nameof(LoadingBackgroundColor), typeof(Color), typeof(UmbrellaActivityInidicatorView));

	/// <summary>
	/// The loading foreground color property
	/// </summary>
	public static BindableProperty LoadingForegroundColorProperty = BindableProperty.Create(nameof(LoadingForegroundColor), typeof(Color), typeof(UmbrellaActivityInidicatorView));

	/// <summary>
	/// Gets or sets a value indicating whether this instance is loading.
	/// </summary>
	public bool IsLoading
	{
		get => (bool)GetValue(IsLoadingProperty);
		set => SetValue(IsLoadingProperty, value);
	}

	/// <summary>
	/// Gets or sets the color of the loading background.
	/// </summary>
	public Color LoadingBackgroundColor
	{
		get => (Color)GetValue(LoadingBackgroundColorProperty);
		set => SetValue(LoadingBackgroundColorProperty, value);
	}

	/// <summary>
	/// Gets or sets the color of the loading foreground.
	/// </summary>
	public Color LoadingForegroundColor
	{
		get => (Color)GetValue(LoadingForegroundColorProperty);
		set => SetValue(LoadingForegroundColorProperty, value);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaActivityInidicatorView"/> class.
	/// </summary>
	public UmbrellaActivityInidicatorView()
	{
		InitializeComponent();

		ILoadingScreenService loadingScreenUtility = UmbrellaXamarinServices.GetService<ILoadingScreenService>();

		loadingScreenUtility.OnStateChanged += state => Device.BeginInvokeOnMainThread(() => IsLoading = state switch
		{
			LoadingScreenState.Visible => true,
			LoadingScreenState.Hidden => false,
			_ => false
		});
	}
}