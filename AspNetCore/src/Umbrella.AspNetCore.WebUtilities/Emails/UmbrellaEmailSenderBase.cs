using CommunityToolkit.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Umbrella.Utilities.Email;
using Umbrella.Utilities.Email.Abstractions;

namespace Umbrella.AspNetCore.WebUtilities.Emails;

/// <summary>
/// Serves as the base class for types that send emails.
/// </summary>
public abstract class UmbrellaEmailSenderBase
{
	/// <summary>
	/// Gets the logger.
	/// </summary>
	protected ILogger Logger { get; }

	/// <summary>
	/// Gets the email sender.
	/// </summary>
	protected IEmailSender EmailSender { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaEmailSenderBase"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="emailSender">The email sender.</param>
	protected UmbrellaEmailSenderBase(
		ILogger logger,
		IEmailSender emailSender)
	{
		Logger = logger;
		EmailSender = emailSender;
	}

	/// <summary>
	/// Sends an email using the specified body content.
	/// </summary>
	/// <param name="content">The body content of the email.</param>
	/// <param name="email">The destination email address. Multiple email addresses can be specified using a comma-delimited value.</param>
	/// <param name="subject">The email subject.</param>
	/// <param name="fromAddress">The sender's address. If not specified, the <see cref="IEmailSender"/> will use the default email address from its configuration settings.</param>
	/// <param name="attachments">The optional list of attachements for the email.</param>
	/// <param name="ccList">The list of email addresses to be added as CCs.</param>
	/// <param name="bccList">The list of email addresses to be added as BCCs.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A <see cref="Task"/> which completes when the email has been sent to the system which ultimately sends the email.</returns>
	protected async Task SendEmailContentAsync(string content, string email, string subject, string? fromAddress = null, IEnumerable<EmailAttachment>? attachments = null, IEnumerable<string>? ccList = null, IEnumerable<string>? bccList = null, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNullOrEmpty(content);

		await EmailSender.SendEmailAsync(email, subject, content, fromAddress, attachments, ccList, bccList, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Serializes a collection of email addresses for logging.
	/// </summary>
	/// <param name="collection">The collection to serialize.</param>
	/// <returns>The serialized collection.</returns>
	protected static string? SerializeStringCollection(IEnumerable<string>? collection)
		=> collection is null
			? null
			: string.Join(",", collection
				.Where(x => !string.IsNullOrWhiteSpace(x))
				.Select(x => x.Trim()));

	/// <summary>
	/// Serializes a collection of email attachments for logging.
	/// </summary>
	/// <param name="attachments">The attachments to serialize.</param>
	/// <returns>The serialized attachments.</returns>
	protected static string? SerializeAttachments(IEnumerable<EmailAttachment>? attachments)
		=> attachments is null
			? null
			: string.Join(",", attachments.Select(x => $"{x.FileName} ({x.ContentType})"));

	/// <summary>
	/// Serializes component parameters for logging.
	/// </summary>
	/// <param name="parameters">The parameters to serialize.</param>
	/// <returns>The serialized parameters.</returns>
	protected static string? SerializeParameters(IDictionary<string, object?>? parameters)
		=> parameters is null
			? null
			: string.Join(",", parameters
				.OrderBy(x => x.Key, StringComparer.Ordinal)
				.Select(x => $"{x.Key}={Convert.ToString(x.Value, CultureInfo.InvariantCulture) ?? "null"}"));
}