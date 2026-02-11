
using CommunityToolkit.Diagnostics;
using Umbrella.Utilities.Options.Abstractions;

namespace Umbrella.Utilities.Email.Options;

/// <summary>
/// Options for the <see cref="EmailFactory"/>.
/// </summary>
public class EmailFactoryOptions : ISanitizableUmbrellaOptions, IValidatableUmbrellaOptions
{
	/// <summary>
	/// Gets or sets the virtual path of the folder containing the email templates. This defaults to "~/Content/EmailTemplates/".
	/// </summary>
	public string TemplatesVirtualPath { get; set; } = "~/Content/EmailTemplates/";

	/// <summary>
	/// Gets or sets the HTML string template used for data rows. This defaults to using tr and td table elements.
	/// </summary>
	public string DataRowFormat { get; set; } = "<tr><td>{0}:</td><td>{1}</td></tr>";

	/// <summary>
	/// Gets or sets the token used for a new line, e.g. \r\n, which is used to replace all instances with HTML br tags.
	/// Defaults to <see cref="Environment.NewLine"/>.
	/// </summary>
	public string NewLineToken { get; set; } = Environment.NewLine;

	/// <inheritdoc />
	public void Sanitize()
	{
		TemplatesVirtualPath = TemplatesVirtualPath?.Trim() ?? "";
		DataRowFormat = DataRowFormat?.Trim() ?? "";
		NewLineToken = NewLineToken?.Trim(' ') ?? "";
	}

	/// <summary>
	/// Validates this instance.
	/// </summary>
	/// <exception cref="ArgumentNullException">Thrown if <see cref="DataRowFormat"/> is null.</exception>
	/// <exception cref="ArgumentException">Thrown if <see cref="DataRowFormat"/> is empty or whitespace.</exception>
	/// <exception cref="ArgumentNullException">Thrown if <see cref="TemplatesVirtualPath"/> is null.</exception>
	/// <exception cref="ArgumentException">Thrown if <see cref="TemplatesVirtualPath"/> is empty or whitespace.</exception>
	public void Validate()
	{
		Guard.IsNotNullOrWhiteSpace(DataRowFormat, nameof(DataRowFormat));
		Guard.IsNotNullOrWhiteSpace(TemplatesVirtualPath, nameof(TemplatesVirtualPath));
		Guard.IsNotNullOrEmpty(NewLineToken, nameof(NewLineToken));
	}
}