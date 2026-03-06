using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Logging;
using Umbrella.AspNetCore.WebUtilities.Razor.Abstractions;
using Umbrella.Utilities.Email;
using Umbrella.Utilities.Email.Abstractions;
using Umbrella.WebUtilities.Exceptions;

namespace Umbrella.AspNetCore.WebUtilities.Emails;

/// <summary>
/// Serves as the base class for types that send emails generated using Razor views.
/// </summary>
public abstract class UmbrellaRazorEmailSender : UmbrellaEmailSenderBase
{
	/// <summary>
	/// Gets the Razor view to string renderer.
	/// </summary>
	protected IRazorViewToStringRenderer ViewToStringRenderer { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaRazorEmailSender"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="emailSender">The email sender.</param>
	/// <param name="viewToStringRenderer">The Razor view to string renderer.</param>
	protected UmbrellaRazorEmailSender(
		ILogger logger,
		IEmailSender emailSender,
		IRazorViewToStringRenderer viewToStringRenderer)
		: base(logger, emailSender)
	{
		ViewToStringRenderer = viewToStringRenderer;
	}

	/// <summary>
	/// Gets the full relative path to the specified <paramref name="viewName"/>.
	/// </summary>
	/// <param name="viewName">The view name.</param>
	/// <returns>The path to the specified view.</returns>
	/// <remarks>
	/// This should convert the <paramref name="viewName"/> to a relative path,
	/// e.g. MyEmail should be transformed to ~/Views/Emails/MyEmail.cshtml,
	/// which is a path that can be used by the Razor view engine to find the view.
	/// </remarks>
	protected abstract string GetFullViewPath(string viewName);

	/// <summary>
	/// Sends an email using the specified parameters.
	/// </summary>
	/// <typeparam name="T">The type of the email model.</typeparam>
	/// <param name="model">The model.</param>
	/// <param name="email">The destination email address. Multiple email addresses can be specified using a comma-delimited value.</param>
	/// <param name="subject">The email subject.</param>
	/// <param name="viewNameOrPath">The relative path or name of the razor view.</param>
	/// <param name="fromAddress">The sender's address. If not specified, the <see cref="IEmailSender"/> will use the default email address from its configuration settings.</param>
	/// <param name="attachments">The optional list of attachements for the email.</param>
	/// <param name="ccList">The list of email addresses to be added as CCs.</param>
	/// <param name="bccList">The list of email addresses to be added as BCCs.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A <see cref="Task"/> which completes when the email has been sent to the system which ultimately sends the email.</returns>
	/// <exception cref="UmbrellaWebException">Thrown if there is an error sending the email.</exception>
	protected async Task SendEmailAsync<T>(T model, string email, string subject, string viewNameOrPath, string? fromAddress = null, IEnumerable<EmailAttachment>? attachments = null, IEnumerable<string>? ccList = null, IEnumerable<string>? bccList = null, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNullOrEmpty(viewNameOrPath);
		string? viewPath = null;

		try
		{
			viewPath = viewNameOrPath.StartsWith('~') ? viewNameOrPath : GetFullViewPath(viewNameOrPath);

			string content = await ViewToStringRenderer.RenderViewToStringAsync(viewPath, model, cancellationToken: cancellationToken).ConfigureAwait(false);

			await SendEmailContentAsync(content, email, subject, fromAddress, attachments, ccList, bccList, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new
		{
			model,
			email,
			subject,
			viewNameOrPath,
			viewPath,
			fromAddress,
			attachments = SerializeAttachments(attachments),
			ccList = SerializeStringCollection(ccList),
			bccList = SerializeStringCollection(bccList)
		}))
		{
			throw new UmbrellaWebException($"There has been an error sending the '{subject}' email.", exc);
		}
	}
}