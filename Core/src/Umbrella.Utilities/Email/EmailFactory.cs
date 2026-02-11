
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Logging;
using Umbrella.Utilities.Data.Abstractions;
using Umbrella.Utilities.Dating.Abstractions;
using Umbrella.Utilities.Email.Abstractions;
using Umbrella.Utilities.Email.Options;
using Umbrella.Utilities.Exceptions;
using Umbrella.Utilities.Hosting.Abstractions;

namespace Umbrella.Utilities.Email;

/// <summary>
/// A generic class to aid in building emails based on HTML generated templates stored as static files on disk.
/// </summary>
/// <seealso cref="IEmailFactory" />
public class EmailFactory : IEmailFactory
{
	#region Private Static Members
	private static readonly CultureInfo _cultureInfo = new("en-GB");
	private static Dictionary<string, string>? _emailTemplateDictionary;
	#endregion

	#region Private Members
	private readonly ILogger _log;
	private readonly IDateTimeProvider _dateTimeProvider;
	private readonly ILogger<EmailContent> _emailContentLog;
	private readonly IDataLookupNormalizer _lookupNormalizer;
	private readonly EmailFactoryOptions _options;
	private readonly IUmbrellaHostingEnvironment _hostingEnvironment;
	private readonly string _encodedNewLineToken;
	#endregion

	#region Constructors					
	/// <summary>
	/// Initializes a new instance of the <see cref="EmailFactory"/> class.
	/// </summary>
	/// <param name="loggerFactory">The logger factory.</param>
	/// <param name="logger">The logger.</param>
	/// <param name="dateTimeProvider">The date time provider.</param>
	/// <param name="lookupNormalizer">The lookup normalizer.</param>
	/// <param name="options">The options.</param>
	/// <param name="hostingEnvironment">The hosting environment.</param>
	/// <exception cref="UmbrellaException">There has been a problem initializing the email builder.</exception>
	public EmailFactory(
		ILoggerFactory loggerFactory,
		ILogger<EmailFactory> logger,
		IDateTimeProvider dateTimeProvider,
		IDataLookupNormalizer lookupNormalizer,
		EmailFactoryOptions options,
		IUmbrellaHostingEnvironment hostingEnvironment)
	{
		_emailContentLog = loggerFactory.CreateLogger<EmailContent>();
		_log = logger;
		_dateTimeProvider = dateTimeProvider;
		_lookupNormalizer = lookupNormalizer;
		_options = options;
		_hostingEnvironment = hostingEnvironment;
		_encodedNewLineToken = HtmlEncoder.Default.Encode(_options.NewLineToken);

		try
		{
			string? absolutePath = _hostingEnvironment.MapPath(options.TemplatesVirtualPath);

			Guard.IsNotNullOrEmpty(absolutePath);

			var dicItems = new Dictionary<string, string>();

			foreach (string filename in Directory.EnumerateFiles(absolutePath, "*.html", SearchOption.TopDirectoryOnly))
			{
				// Read all template files into memory and store in the dictionary
				using var fileStream = new FileStream(filename, FileMode.Open);
				using var reader = new StreamReader(fileStream);

				string template = reader.ReadToEnd();

				dicItems.Add(_lookupNormalizer.Normalize(Path.GetFileNameWithoutExtension(filename)), template);
			}

			_emailTemplateDictionary = dicItems;
		}
		catch (Exception exc) when (_log.WriteError(exc, new { options }))
		{
			throw new UmbrellaException("There has been a problem initializing the email builder.", exc);
		}
	}
	#endregion

	#region IEmailBuilder Members

	/// <inheritdoc />
	public EmailContent CreateEmail(string source = "GenericTemplate", bool isRawHtml = false)
	{
		Guard.IsNotNullOrWhiteSpace(source, nameof(source));

		try
		{
			var builder = isRawHtml
					? new StringBuilder(source)
					: new StringBuilder(_emailTemplateDictionary![_lookupNormalizer.Normalize(source)]);

			// Make sure the date is shown in the correct format for the current user
			_ = builder.Replace("{datetime}", _dateTimeProvider.Now.ToString(_cultureInfo));

			return new EmailContent(_emailContentLog, builder, _options.DataRowFormat, _options.NewLineToken, _encodedNewLineToken);
		}
		catch (Exception exc) when (_log.WriteError(exc, new { source, isRawHtml }))
		{
			throw new UmbrellaException("There was a problem initializing the builder using the specified options.", exc);
		}
	}
	#endregion
}