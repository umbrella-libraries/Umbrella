


using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Logging;
using Umbrella.Utilities.Email.Abstractions;
using Umbrella.Utilities.Exceptions;

namespace Umbrella.Utilities.Email;

/// <summary>
/// This class encapsulates the content of an email message and allows it to be built based upon the template
/// specified when creating this instance via the <see cref="IEmailFactory"/> implementation.
/// </summary>
public class EmailContent
{
	#region Private Members
	private readonly ILogger _log;
	private readonly StringBuilder _builder;
	private StringBuilder? _rowsBuilder;
	private readonly string _dataRowFormat;
	private readonly string _newLineToken;
	private readonly string _encodedNewLineToken;
	#endregion

	#region Constructors
	internal EmailContent(
		ILogger<EmailContent> logger,
		StringBuilder builder,
		string dataRowFormat,
		string newLineToken,
		string encodedNewLineToken)
	{
		_log = logger;
		_builder = builder;
		_dataRowFormat = dataRowFormat;
		_newLineToken = newLineToken;
		_encodedNewLineToken = encodedNewLineToken;
	}
	#endregion

	#region Public Methods
	/// <summary>
	/// Appends a data row to the email data rows builder. The data rows builder will ultimately be substituted for all instances of {rows} in the email template.
	/// </summary>
	/// <param name="name">The name of the data item, e.g. First Name</param>
	/// <param name="value">The value of the data item, e.g. Rich</param>
	/// <param name="htmlEncode">Specifies whether the <paramref name="value"/> will be HTML encoded.</param>
	/// <param name="replaceNewLines">Determine if new lines are converted to br tags.</param>
	/// <returns>The <see cref="EmailFactory"/>.</returns>
	/// <exception cref="UmbrellaException">There was a problem appending the data row with the specified name and value.</exception>
	public virtual EmailContent AppendRow(string name, string value, bool htmlEncode = true, bool replaceNewLines = true)
	{
		try
		{
			_rowsBuilder ??= new StringBuilder();

			_ = _rowsBuilder.AppendFormat(CultureInfo.InvariantCulture, _dataRowFormat, name, SanitizeValue(value, htmlEncode, replaceNewLines));

			return this;
		}
		catch (Exception exc) when (_log.WriteError(exc, new { name, value }))
		{
			throw new UmbrellaException("There was a problem appending the data row with the specified name and value.", exc);
		}
	}

	/// <summary>
	/// Appends the data rows specified in the <paramref name="source"/> collection.
	/// </summary>
	/// <typeparam name="T">The type of the data item being appended.</typeparam>
	/// <param name="source">The source collection containing instances of <typeparamref name="T"/>.</param>
	/// <param name="rowsTokenName">Name of the rows token in the email template to be replaced by these data rows.</param>
	/// <param name="rowFormat">The row format.</param>
	/// <param name="keySelector">The key selector.</param>
	/// <param name="valueSelector">The value selector.</param>
	/// <returns>The <see cref="EmailContent"/>.</returns>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="source"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="rowsTokenName"/> is null.</exception>
	/// <exception cref="ArgumentException">Thrown if <paramref name="rowsTokenName"/> is empty or whitespace.</exception>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="rowFormat"/> is null.</exception>
	/// <exception cref="ArgumentException">Thrown if <paramref name="rowFormat"/> is empty or whitespace.</exception>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="keySelector"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="valueSelector"/> is null.</exception>
	/// <exception cref="UmbrellaException">There was a problem appending the specified data rows.</exception>
	public virtual EmailContent AppendDataRows<T>(IEnumerable<T> source, string rowsTokenName, string rowFormat, Func<T, string> keySelector, Func<T, string> valueSelector)
	{
		Guard.IsNotNull(source, nameof(source));
		Guard.IsNotNullOrWhiteSpace(rowsTokenName, nameof(rowsTokenName));
		Guard.IsNotNullOrWhiteSpace(rowFormat, nameof(rowFormat));
		Guard.IsNotNull(keySelector, nameof(keySelector));
		Guard.IsNotNull(valueSelector, nameof(valueSelector));

		try
		{
			var builder = new StringBuilder();

			foreach (T item in source)
			{
				_ = builder.AppendFormat(CultureInfo.InvariantCulture, rowFormat, keySelector(item), valueSelector is not null ? valueSelector(item) : string.Empty);
			}

			_ = ReplaceToken(rowsTokenName, builder.ToString());

			return this;
		}
		catch (Exception exc) when (_log.WriteError(exc, new { rowsTokenName, rowFormat }))
		{
			throw new UmbrellaException("There was a problem appending the specified data rows.", exc);
		}
	}

	/// <summary>
	/// Replaces the specified <paramref name="tokenName"/> with the specified <paramref name="value"/>.
	/// </summary>
	/// <param name="tokenName">Name of the token.</param>
	/// <param name="value">The value to replace the token.</param>
	/// <param name="htmlEncode">Specifies whether the <paramref name="value"/> will be HTML encoded.</param>
	/// <param name="replaceNewLines">Determine if new lines are converted to br tags.</param>
	/// <returns>The <see cref="EmailContent"/>.</returns>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="tokenName"/> is null.</exception>
	/// <exception cref="ArgumentException">Thrown if <paramref name="tokenName"/> is empty or whitespace.</exception>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="value"/> is null.</exception>
	/// <exception cref="ArgumentException">Thrown if <paramref name="value"/> is empty or whitespace.</exception>
	/// <exception cref="UmbrellaException">There has been a problem replacing the specified token with the specified value.</exception>
	public virtual EmailContent ReplaceToken(string tokenName, string value, bool htmlEncode = true, bool replaceNewLines = true)
	{
		Guard.IsNotNullOrWhiteSpace(tokenName, nameof(tokenName));

		try
		{
			_ = _builder.Replace("{{" + tokenName + "}}", SanitizeValue(value, htmlEncode, replaceNewLines));

			return this;
		}
		catch (Exception exc) when (_log.WriteError(exc, new { tokenName }))
		{
			throw new UmbrellaException("There has been a problem replacing the specified token with the specified value.", exc);
		}
	}
	#endregion

	#region Overridden Methods
	/// <summary>
	/// Converts the email content to a string.
	/// </summary>
	/// <returns>
	/// A <see cref="string" /> that represents this instance.
	/// </returns>
	/// <exception cref="UmbrellaException">There was a problem outputting this instance to a string.</exception>
	public override string ToString()
	{
		try
		{
			return _rowsBuilder is not null ? _builder.Replace("{{rows}}", _rowsBuilder.ToString()).ToString() : _builder.ToString();
		}
		catch (Exception exc) when (_log.WriteError(exc))
		{
			throw new UmbrellaException("There was a problem outputting this instance to a string.", exc);
		}
	}
	#endregion

	#region Protected Methods		
	/// <summary>
	/// Sanitizes the value.
	/// </summary>
	/// <param name="value">The value.</param>
	/// <param name="htmlEncode">Determines whether or not the value is HTML encoded.</param>
	/// <param name="replaceNewLines">Determine if new lines are converted to br tags.</param>
	/// <returns></returns>
	protected string SanitizeValue(string value, bool htmlEncode, bool replaceNewLines)
	{
		if (string.IsNullOrWhiteSpace(value))
			return "";

		string newLineToken = _newLineToken;

		if (htmlEncode)
		{
			value = HtmlEncoder.Default.Encode(value);
			newLineToken = _encodedNewLineToken;
		}

		if (replaceNewLines)
#if NET6_0_OR_GREATER
			value = value.Replace(newLineToken, "<br />", StringComparison.Ordinal);
#else
			value = value.Replace(newLineToken, "<br />");
#endif

        return value;
	}
#endregion
}