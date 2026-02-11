


using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Logging;
using System.Net.Mail;
using Umbrella.Utilities.Email.Abstractions;
using Umbrella.Utilities.Email.Options;
using Umbrella.Utilities.Exceptions;
using Umbrella.Utilities.Extensions;

namespace Umbrella.Utilities.Email;

// TODO: Migrate to MailKit or newer alternative as SmtpClient has been marked as obsolete by Microsoft.
// Remove dependency from System.Net.Mail.Attachment in favour of a custom abstraction.

/// <summary>
/// An implementation of the <see cref="IEmailSender" /> abstraction that either sends emails
/// using a configured SMTP server or saves them to a folder on disk as .eml files. Configuration is specified using
/// the singleton <see cref="EmailSenderOptions" /> configured with the application's DI container.
/// </summary>
public class EmailSender : IEmailSender
{
	private static volatile bool _pickupDirectoryLocationCreated;
	private readonly ILogger<EmailSender> _logger;
	private readonly EmailSenderOptions _options;

	/// <summary>
	/// Create a new instance.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="options">The options.</param>
	public EmailSender(
		ILogger<EmailSender> logger,
		EmailSenderOptions options)
	{
		_logger = logger;
		_options = options;
	}

	/// <inheritdoc />
	public async Task SendEmailAsync(string email, string subject, string body, string? fromAddress = null, IEnumerable<EmailAttachment>? attachments = null, IEnumerable<string>? ccList = null, IEnumerable<string>? bccList = null, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNullOrWhiteSpace(email);
		Guard.IsNotNullOrWhiteSpace(subject);
		Guard.IsNotNullOrWhiteSpace(body);

		try
		{
			IReadOnlyCollection<string> DetermineRecipients(IEnumerable<string> emails)
			{
				HashSet<string> lstRecipientEmail = new(StringComparer.OrdinalIgnoreCase);

				foreach (string email in emails)
				{
					string[] emailParts = email.Split(',');

					foreach (string part in emailParts)
					{
						if (!string.IsNullOrWhiteSpace(part))
						{
							if (_options.RedirectRecipientEmailsList.Count > 0 && !_options.EmailRecipientDomainWhiteList.Any(x => part.EndsWith(x, StringComparison.OrdinalIgnoreCase)))
							{
								lstRecipientEmail.AddRange(_options.RedirectRecipientEmailsList);
								continue;
							}

							if (_options.EmailRecipientDomainWhiteList.Count > 0 && !_options.EmailRecipientDomainWhiteList.Any(x => part.EndsWith(x, StringComparison.OrdinalIgnoreCase)))
							{
								// We don't want to send the email to this recipient
								continue;
							}

							lstRecipientEmail.Add(part);
						}
					}
				}

				return lstRecipientEmail;
			}

			using var client = new SmtpClient();

			switch (_options.DeliveryMethod)
			{
				case EmailSenderDeliveryMode.Network:
					{
						client.DeliveryMethod = SmtpDeliveryMethod.Network;
						client.Host = _options.Host!;
						client.Port = _options.Port;
						client.EnableSsl = _options.SecureServerConnection;

						if (!string.IsNullOrWhiteSpace(_options.UserName))
							client.Credentials = new NetworkCredential { UserName = _options.UserName, Password = _options.Password };

						email = string.Join(",", DetermineRecipients(new[] { email }));

						if (string.IsNullOrEmpty(email))
							return;

						if (ccList is not null)
							ccList = DetermineRecipients(ccList);

						if (bccList is not null)
							bccList = DetermineRecipients(bccList);

						break;
					}
				case EmailSenderDeliveryMode.SpecifiedPickupDirectory:
					{
						client.DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory;
						client.PickupDirectoryLocation = _options.PickupDirectoryLocation;

						if (!_pickupDirectoryLocationCreated && !string.IsNullOrEmpty(_options.PickupDirectoryLocation))
						{
							Directory.CreateDirectory(_options.PickupDirectoryLocation);
							_pickupDirectoryLocationCreated = true;
						}

						break;
					}
				default:
					throw new NotSupportedException($"Only {nameof(SmtpDeliveryMethod.Network)} and {nameof(SmtpDeliveryMethod.SpecifiedPickupDirectory)} are supported as delivery methods.");
			}

			string? senderAddress = fromAddress?.TrimToLowerInvariant() ?? _options.DefaultFromAddress;

			if (string.IsNullOrEmpty(senderAddress))
				throw new InvalidOperationException("The sender address cannot be determined.");

			using var message = new MailMessage(senderAddress, email)
			{
				Subject = subject,
				Body = body,
				IsBodyHtml = true
			};

			if (attachments is not null)
			{
				foreach (EmailAttachment attachment in attachments)
				{
					if (attachment.Content.Length > 0 && !string.IsNullOrEmpty(attachment.FileName) && !string.IsNullOrEmpty(attachment.ContentType))
						message.Attachments.Add(new Attachment(attachment.Content, attachment.FileName, attachment.ContentType));
				}
			}

			ccList?.ForEach(message.CC.Add);
			bccList?.ForEach(message.Bcc.Add);

#if NET6_0_OR_GREATER
			await client.SendMailAsync(message, cancellationToken).ConfigureAwait(false);
#else
			await client.SendMailAsync(message).ConfigureAwait(false);
#endif
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { email, subject, body, fromAddress, ccList, bccList }))
		{
			throw new UmbrellaException("There has been a problem sending the email.", exc);
		}
	}
}