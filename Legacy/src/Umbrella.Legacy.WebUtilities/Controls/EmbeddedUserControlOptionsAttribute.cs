


using System.Web.UI;

namespace Umbrella.Legacy.WebUtilities.Controls;

/// <summary>
/// An attribute that can be applied to ASP.NET user controls to allow them to be embedded as resources inside an assembly.
/// This is primarily
/// </summary>
/// <seealso cref="Attribute" />
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class EmbeddedUserControlOptionsAttribute : Attribute
{
	/// <summary>
	/// Gets or sets a value indicating whether view state is enabled for the user control this attribute targets.
	/// </summary>
	public bool EnableViewState { get; }

	/// <summary>
	/// Gets or sets the client identifier mode.
	/// </summary>
	public ClientIDMode ClientIDMode { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="EmbeddedUserControlOptionsAttribute"/> class.
	/// </summary>
	/// <param name="enableViewState">Specifies if view state is enabled for the target user control.</param>
	/// <param name="clientIdMode">The client identifier mode.</param>
	public EmbeddedUserControlOptionsAttribute(bool enableViewState, ClientIDMode clientIdMode)
	{
		EnableViewState = enableViewState;
		ClientIDMode = clientIdMode;
	}
}