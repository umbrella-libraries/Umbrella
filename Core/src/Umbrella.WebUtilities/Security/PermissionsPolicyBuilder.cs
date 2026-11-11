using Umbrella.Utilities.Extensions;

namespace Umbrella.WebUtilities.Security;

/// <summary>
/// A builder used to construct Permissions Policy and Feature Policy HTTP headers.
/// </summary>
public class PermissionsPolicyBuilder
{
	private readonly Dictionary<string, HashSet<string>> _permissionsDictionary = [with(StringComparer.OrdinalIgnoreCase)];

	/// <summary>
	/// Initializes a new instance of the <see cref="PermissionsPolicyBuilder"/> class.
	/// </summary>
	/// <param name="emitAllAsDefaults">If set to <see langword="true" />, all policies will be added with no values.</param>
	public PermissionsPolicyBuilder(bool emitAllAsDefaults = false)
	{
		if (!emitAllAsDefaults)
			return;

		_ = Accelerometer();
		_ = AmbientLightSensor();
		_ = Autoplay();
		_ = Battery();
		_ = Camera();
		_ = ClipboardRead();
		_ = ClipboardWrite();
		_ = CrossOriginIsolated();
		_ = DisplayCapture();
		_ = DocumentDomain();
		_ = EncryptedMedia();
		_ = ExecutionWhileNotRendered();
		_ = ExecutionWhileOutOfViewport();
		_ = Fullscreen();
		_ = Gamepad();
		_ = Geolocation();
		_ = Gyroscope();
		_ = KeyboardMap();
		_ = Magnetometer();
		_ = Microphone();
		_ = Midi();
		_ = NavigationOverride();
		_ = Payment();
		_ = PictureInPicture();
		_ = PublickeyCredentialsGet();
		_ = ScreenWakeLock();
		_ = SpeakerSelection();
		_ = SyncXhr();
		_ = Usb();
		_ = WebShare();
		_ = XrSpatialTracking();
	}

	/// <summary>
	/// Adds a policy for <c>accelerometer</c>
	/// </summary>
	/// <param name="values">The values.</param>
	/// <returns>The policy builder.</returns>
	public PermissionsPolicyBuilder Accelerometer(params string[] values) => AddNamedPolicy("accelerometer", values);

	/// <summary>
	/// Adds a policy for <c>ambient-light-sensor</c>
	/// </summary>
	/// <param name="values">The values.</param>
	/// <returns>The policy builder.</returns>
	public PermissionsPolicyBuilder AmbientLightSensor(params string[] values) => AddNamedPolicy("ambient-light-sensor", values);

	/// <summary>
	/// Adds a policy for <c>autoplay</c>
	/// </summary>
	/// <param name="values">The values.</param>
	/// <returns>The policy builder.</returns>
	public PermissionsPolicyBuilder Autoplay(params string[] values) => AddNamedPolicy("autoplay", values);

	/// <summary>
	/// Adds a policy for <c>battery</c>
	/// </summary>
	/// <param name="values">The values.</param>
	/// <returns>The policy builder.</returns>
	public PermissionsPolicyBuilder Battery(params string[] values) => AddNamedPolicy("battery", values);

	/// <summary>
	/// Adds a policy for <c>camera</c>
	/// </summary>
	/// <param name="values">The values.</param>
	/// <returns>The policy builder.</returns>
	public PermissionsPolicyBuilder Camera(params string[] values) => AddNamedPolicy("camera", values);

	/// <summary>
	/// Adds a policy for <c>clipboard-read</c>
	/// </summary>
	/// <param name="values">The values.</param>
	/// <returns>The policy builder.</returns>
	public PermissionsPolicyBuilder ClipboardRead(params string[] values) => AddNamedPolicy("clipboard-read", values);

	/// <summary>
	/// Adds a policy for <c>clipboard-write</c>
	/// </summary>
	/// <param name="values">The values.</param>
	/// <returns>The policy builder.</returns>
	public PermissionsPolicyBuilder ClipboardWrite(params string[] values) => AddNamedPolicy("clipboard-write", values);

	/// <summary>
	/// Adds a policy for <c>cross-origin-isolated</c>
	/// </summary>
	/// <param name="values">The values.</param>
	/// <returns>The policy builder.</returns>
	public PermissionsPolicyBuilder CrossOriginIsolated(params string[] values) => AddNamedPolicy("cross-origin-isolated", values);

	/// <summary>
	/// Adds a policy for <c>display-capture</c>
	/// </summary>
	/// <param name="values">The values.</param>
	/// <returns>The policy builder.</returns>
	public PermissionsPolicyBuilder DisplayCapture(params string[] values) => AddNamedPolicy("display-capture", values);

	/// <summary>
	/// Adds a policy for <c>document-domain</c>
	/// </summary>
	/// <param name="values">The values.</param>
	/// <returns>The policy builder.</returns>
	public PermissionsPolicyBuilder DocumentDomain(params string[] values) => AddNamedPolicy("document-domain", values);

	/// <summary>
	/// Adds a policy for <c>encrypted-media</c>
	/// </summary>
	/// <param name="values">The values.</param>
	/// <returns>The policy builder.</returns>
	public PermissionsPolicyBuilder EncryptedMedia(params string[] values) => AddNamedPolicy("encrypted-media", values);

	/// <summary>
	/// Adds a policy for <c>execution-while-not-rendered</c>
	/// </summary>
	/// <param name="values">The values.</param>
	/// <returns>The policy builder.</returns>
	public PermissionsPolicyBuilder ExecutionWhileNotRendered(params string[] values) => AddNamedPolicy("execution-while-not-rendered", values);

	/// <summary>
	/// Adds a policy for <c>execution-while-out-of-viewport</c>
	/// </summary>
	/// <param name="values">The values.</param>
	/// <returns>The policy builder.</returns>
	public PermissionsPolicyBuilder ExecutionWhileOutOfViewport(params string[] values) => AddNamedPolicy("execution-while-out-of-viewport", values);

	/// <summary>
	/// Adds a policy for <c>fullscreen</c>
	/// </summary>
	/// <param name="values">The values.</param>
	/// <returns>The policy builder.</returns>
	public PermissionsPolicyBuilder Fullscreen(params string[] values) => AddNamedPolicy("fullscreen", values);

	/// <summary>
	/// Adds a policy for <c>gamepad</c>
	/// </summary>
	/// <param name="values">The values.</param>
	/// <returns>The policy builder.</returns>
	public PermissionsPolicyBuilder Gamepad(params string[] values) => AddNamedPolicy("gamepad", values);

	/// <summary>
	/// Adds a policy for <c>geolocation</c>
	/// </summary>
	/// <param name="values">The values.</param>
	/// <returns>The policy builder.</returns>
	public PermissionsPolicyBuilder Geolocation(params string[] values) => AddNamedPolicy("geolocation", values);

	/// <summary>
	/// Adds a policy for <c>gyroscope</c>
	/// </summary>
	/// <param name="values">The values.</param>
	/// <returns>The policy builder.</returns>
	public PermissionsPolicyBuilder Gyroscope(params string[] values) => AddNamedPolicy("gyroscope", values);

	/// <summary>
	/// Adds a policy for <c>keyboard-map</c>
	/// </summary>
	/// <param name="values">The values.</param>
	/// <returns>The policy builder.</returns>
	public PermissionsPolicyBuilder KeyboardMap(params string[] values) => AddNamedPolicy("keyboard-map", values);

	/// <summary>
	/// Adds a policy for <c>magnetometer</c>
	/// </summary>
	/// <param name="values">The values.</param>
	/// <returns>The policy builder.</returns>
	public PermissionsPolicyBuilder Magnetometer(params string[] values) => AddNamedPolicy("magnetometer", values);

	/// <summary>
	/// Adds a policy for <c>microphone</c>
	/// </summary>
	/// <param name="values">The values.</param>
	/// <returns>The policy builder.</returns>
	public PermissionsPolicyBuilder Microphone(params string[] values) => AddNamedPolicy("microphone", values);

	/// <summary>
	/// Adds a policy for <c>midi</c>
	/// </summary>
	/// <param name="values">The values.</param>
	/// <returns>The policy builder.</returns>
	public PermissionsPolicyBuilder Midi(params string[] values) => AddNamedPolicy("midi", values);

	/// <summary>
	/// Adds a policy for <c>navigation-override</c>
	/// </summary>
	/// <param name="values">The values.</param>
	/// <returns>The policy builder.</returns>
	public PermissionsPolicyBuilder NavigationOverride(params string[] values) => AddNamedPolicy("navigation-override", values);

	/// <summary>
	/// Adds a policy for <c>payment</c>
	/// </summary>
	/// <param name="values">The values.</param>
	/// <returns>The policy builder.</returns>
	public PermissionsPolicyBuilder Payment(params string[] values) => AddNamedPolicy("payment", values);

	/// <summary>
	/// Adds a policy for <c>picture-in-picture</c>
	/// </summary>
	/// <param name="values">The values.</param>
	/// <returns>The policy builder.</returns>
	public PermissionsPolicyBuilder PictureInPicture(params string[] values) => AddNamedPolicy("picture-in-picture", values);

	/// <summary>
	/// Adds a policy for <c>publickey-credentials-get</c>
	/// </summary>
	/// <param name="values">The values.</param>
	/// <returns>The policy builder.</returns>
	public PermissionsPolicyBuilder PublickeyCredentialsGet(params string[] values) => AddNamedPolicy("publickey-credentials-get", values);

	/// <summary>
	/// Adds a policy for <c>screen-wake-lock</c>
	/// </summary>
	/// <param name="values">The values.</param>
	/// <returns>The policy builder.</returns>
	public PermissionsPolicyBuilder ScreenWakeLock(params string[] values) => AddNamedPolicy("screen-wake-lock", values);

	/// <summary>
	/// Adds a policy for <c>speaker-selection</c>
	/// </summary>
	/// <param name="values">The values.</param>
	/// <returns>The policy builder.</returns>
	public PermissionsPolicyBuilder SpeakerSelection(params string[] values) => AddNamedPolicy("speaker-selection", values);

	/// <summary>
	/// Adds a policy for <c>sync-xhr</c>
	/// </summary>
	/// <param name="values">The values.</param>
	/// <returns>The policy builder.</returns>
	public PermissionsPolicyBuilder SyncXhr(params string[] values) => AddNamedPolicy("sync-xhr", values);

	/// <summary>
	/// Adds a policy for <c>usb</c>
	/// </summary>
	/// <param name="values">The values.</param>
	/// <returns>The policy builder.</returns>
	public PermissionsPolicyBuilder Usb(params string[] values) => AddNamedPolicy("usb", values);

	/// <summary>
	/// Adds a policy for <c>web-share</c>
	/// </summary>
	/// <param name="values">The values.</param>
	/// <returns>The policy builder.</returns>
	public PermissionsPolicyBuilder WebShare(params string[] values) => AddNamedPolicy("web-share", values);

	/// <summary>
	/// Adds a policy for <c>xr-spatial-tracking</c>
	/// </summary>
	/// <param name="values">The values.</param>
	/// <returns>The policy builder.</returns>
	public PermissionsPolicyBuilder XrSpatialTracking(params string[] values) => AddNamedPolicy("xr-spatial-tracking", values);

	/// <summary>
	/// Adds the named policy.
	/// </summary>
	/// <param name="name">The name.</param>
	/// <param name="values">The values.</param>
	/// <returns></returns>
	public PermissionsPolicyBuilder AddNamedPolicy(string name, params string[] values)
	{
		_permissionsDictionary[name.TrimToLowerInvariant()] = new HashSet<string>(values.Select(x => x.TrimToLowerInvariant()), StringComparer.Ordinal);

		return this;
	}

	/// <inheritdoc/>
	public override string ToString() => ToPermissionsPolicyString();

	/// <summary>
	/// Outputs a string that can be used for the Permissions-Policy HTTP Header.
	/// </summary>
	/// <returns>The policy string.</returns>
	public string ToPermissionsPolicyString() => string.Join(",", _permissionsDictionary.OrderBy(x => x.Key).Select(x =>
	{
		string values = x.Value.Count is 0 ? "" : string.Join(" ", x.Value.Select(x => x switch
		{
			not "none" and not "self" => $"\"{x}\"",
			_ => x
		}));

		return $"{x.Key}=({values})";
	}));

	/// <summary>
	/// Outputs a string that can be used for the Feature-Policy HTTP Header.
	/// </summary>
	/// <returns>The policy string.</returns>
	public string ToFeaturePolicyString() => string.Join(";", _permissionsDictionary.OrderBy(x => x.Key).Select(x =>
	{
		string values = x.Value.Count is 0 ? "'none'" : string.Join(" ", x.Value.Select(x => x switch
		{
			"none" or "self" => $"'{x}'",
			_ => x
		}));

		return $"{x.Key} {values}";
	}));
}