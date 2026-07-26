using System.Text;
using System.Web.UI;

namespace Umbrella.Legacy.WebUtilities.Extensions;

/// <summary>
/// Extension methods for use with the <see cref="Control"/> type.
/// </summary>
public static class ControlExtensions
{
	#region Control Finders
	/// <summary>
	/// Recursively searches Control hierarchy for matches for Predicate and returns it as T
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <param name="from"></param>
	/// <param name="predicate"></param>
	/// <returns>The control to be found.</returns>
	public static T? FindFirstControl<T>(this Control from, Predicate<T> predicate) where T : Control
	{
		if (from is null)
			throw new ArgumentNullException(nameof(from));
		if (predicate is null)
			throw new ArgumentNullException(nameof(predicate));

		if (from is T obj && predicate(obj))
			return obj;

		foreach (Control idx in from.Controls)
		{
			T? tmpRetVal = idx.FindFirstControl(predicate);
			if (tmpRetVal is not null)
				return tmpRetVal;
		}

		return null;
	}

	/// <summary>
	/// Recursively searches Control hierarchy for the first control that
	/// matches the type of T and returns it as T
	/// </summary>
	/// <typeparam name="T">The type of the control.</typeparam>
	/// <param name="from"></param>
	/// <returns>The control to be found.</returns>
	public static T? FindFirstControl<T>(this Control from) where T : Control => from.FindFirstControl<T>(x => x is T);

	/// <summary>
	/// Recursively search Control hierarcy for ALL controls that
	/// matches the given Predicate and returns them as T
	/// </summary>
	/// <typeparam name="T">The type of the control.</typeparam>
	/// <param name="from"></param>
	/// <param name="predicate"></param>
	/// <returns>The controls.</returns>
	public static IEnumerable<T> FindControls<T>(this Control from, Predicate<T> predicate) where T : Control
	{
		if (from is null)
			throw new ArgumentNullException(nameof(from));
		if (predicate is null)
			throw new ArgumentNullException(nameof(predicate));

		if (from is T obj)
		{
			if (predicate(obj))
				yield return obj;
		}

		foreach (Control idx in from.Controls)
		{
			foreach (T idxInner in idx.FindControls(predicate))
			{
				yield return idxInner;
			}
		}
	}

	/// <summary>
	/// Recursively search Control hierarcy for ALL controls that
	/// matches the type of T and returns them as T
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <param name="from"></param>
	/// <returns></returns>
	public static IEnumerable<T> FindControls<T>(this Control from) where T : Control => from.FindControls<T>(x => x is T);
	#endregion

	#region Utilities		
	/// <summary>
	/// Renders the specified control to an HTML string.
	/// </summary>
	/// <param name="ctrl">The control.</param>
	/// <returns>The HTML string.</returns>
	public static string ToHtmlString(this Control ctrl) => ctrl.ToStringBuilder().ToString();

	/// <summary>
	/// Renders the HTML of the specified control to a <see cref="StringBuilder"/>
	/// </summary>
	/// <param name="ctrl">The control.</param>
	/// <returns>The <see cref="StringBuilder"/> containing the HTML string.</returns>
	public static StringBuilder ToStringBuilder(this Control ctrl)
	{
		if (ctrl is null)
			throw new ArgumentNullException(nameof(ctrl));

		var sb = new StringBuilder();
		var tw = new StringWriter(sb);
		using var hw = new HtmlTextWriter(tw);

		ctrl.RenderControl(hw);

		return sb;
	}
	#endregion
}
