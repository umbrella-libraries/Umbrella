


using System.Collections.Concurrent;
using System.Reflection;
using System.Web.UI;

namespace Umbrella.Legacy.WebUtilities.Controls;

/// <summary>
/// A UserControl that can be embedded inside an assembly.
/// </summary>
/// <seealso cref="UserControl" />
public class EmbeddedUserControl : UserControl
{
	#region Private Static Members
	private static readonly ConcurrentDictionary<Type, (string content, EmbeddedUserControlOptionsAttribute options)> _cachedControlInfoDictionary = new();
	private static readonly ConcurrentDictionary<string, List<FieldInfo>> _cachedControlFieldsDictionary = new();
	#endregion

	#region Delegates		
	/// <summary>
	/// The on framework initialized delegate invoked by the <see cref="FrameworkInitialize"/> method after it has executed all other code.
	/// </summary>
	protected Action? OnFrameworkInitialized { get; set; }
	#endregion

	#region Overridden Methods
	/// <inheritdoc />
	protected override void FrameworkInitialize()
	{
		base.FrameworkInitialize();

		Type type = GetType();

		var (content, options) = _cachedControlInfoDictionary.GetOrAdd(type, key =>
		{
			using Stream stream = Assembly.GetAssembly(type).GetManifestResourceStream(type, type.Name + ".ascx");
			using var reader = new StreamReader(stream);

			string content = reader.ReadToEnd();

			EmbeddedUserControlOptionsAttribute options = type.GetCustomAttributes(typeof(EmbeddedUserControlOptionsAttribute), false).OfType<EmbeddedUserControlOptionsAttribute>().FirstOrDefault();

			return (content, options);
		});

		Control userControl = Page.ParseControl(content, true);

		if (options is not null)
		{
			EnableViewState = options.EnableViewState;
			ClientIDMode = options.ClientIDMode;
		}

		Controls.Add(userControl);

		EnsureControls();

		OnFrameworkInitialized?.Invoke();
	}
	#endregion

	#region Private Methods
	private void EnsureControls()
	{
		Type type = GetType();

		List<FieldInfo> fields = _cachedControlFieldsDictionary.GetOrAdd(type.FullName, key => fields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Where(x => typeof(Control).IsAssignableFrom(x.FieldType)).ToList());

		// We should have the reflection information we need from the cache now
		foreach (FieldInfo field in fields)
		{
			if (field.GetValue(this) is null)
			{
				Control value = FindControl(field.Name);
				field.SetValue(this, value);
			}
		}
	}
	#endregion
}