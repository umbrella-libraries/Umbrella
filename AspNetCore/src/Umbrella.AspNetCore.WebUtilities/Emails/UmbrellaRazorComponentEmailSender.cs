using CommunityToolkit.Diagnostics;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Umbrella.AspNetCore.WebUtilities.Components.Abstractions;
using Umbrella.Utilities.Email;
using Umbrella.Utilities.Email.Abstractions;
using Umbrella.WebUtilities.Exceptions;

namespace Umbrella.AspNetCore.WebUtilities.Emails;

/// <summary>
/// Serves as the base class for types that send emails generated using Razor components.
/// </summary>
public abstract class UmbrellaRazorComponentEmailSender : UmbrellaEmailSenderBase
{
	/// <summary>
	/// Gets the Razor component to string renderer.
	/// </summary>
	protected IRazorComponentToStringRenderer ComponentToStringRenderer { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaRazorComponentEmailSender"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="emailSender">The email sender.</param>
	/// <param name="componentToStringRenderer">The Razor component to string renderer.</param>
	protected UmbrellaRazorComponentEmailSender(
		ILogger logger,
		IEmailSender emailSender,
		IRazorComponentToStringRenderer componentToStringRenderer)
		: base(logger, emailSender)
	{
		ComponentToStringRenderer = componentToStringRenderer;
	}

	/// <summary>
	/// Sends an email using the specified component.
	/// </summary>
	/// <typeparam name="TComponent">The type of the component.</typeparam>
	/// <param name="email">The destination email address. Multiple email addresses can be specified using a comma-delimited value.</param>
	/// <param name="subject">The email subject.</param>
	/// <param name="parameters">The optional component parameters.</param>
	/// <param name="fromAddress">The sender's address. If not specified, the <see cref="IEmailSender"/> will use the default email address from its configuration settings.</param>
	/// <param name="attachments">The optional list of attachements for the email.</param>
	/// <param name="ccList">The list of email addresses to be added as CCs.</param>
	/// <param name="bccList">The list of email addresses to be added as BCCs.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A <see cref="Task"/> which completes when the email has been sent to the system which ultimately sends the email.</returns>
	/// <exception cref="UmbrellaWebException">Thrown if there is an error sending the email.</exception>
	protected async Task SendEmailAsync<TComponent>(string email, string subject, IDictionary<string, object?>? parameters = null, string? fromAddress = null, IEnumerable<EmailAttachment>? attachments = null, IEnumerable<string>? ccList = null, IEnumerable<string>? bccList = null, CancellationToken cancellationToken = default)
		where TComponent : IComponent
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			string content = await ComponentToStringRenderer.RenderComponentToStringAsync<TComponent>(parameters, cancellationToken).ConfigureAwait(false);

			await SendEmailContentAsync(content, email, subject, fromAddress, attachments, ccList, bccList, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new
		{
			component = typeof(TComponent).Name,
			email,
			subject,
			fromAddress,
			parameters = SerializeParameters(parameters),
			attachments = SerializeAttachments(attachments),
			ccList = SerializeStringCollection(ccList),
			bccList = SerializeStringCollection(bccList)
		}))
		{
			throw new UmbrellaWebException($"There has been an error sending the '{subject}' email.", exc);
		}
	}

	/// <summary>
	/// Sends an email using the specified component and model.
	/// </summary>
	/// <typeparam name="TComponent">The type of the component.</typeparam>
	/// <typeparam name="TModel">The type of the model.</typeparam>
	/// <param name="model">The model.</param>
	/// <param name="email">The destination email address. Multiple email addresses can be specified using a comma-delimited value.</param>
	/// <param name="subject">The email subject.</param>
	/// <param name="parameters">The optional additional component parameters.</param>
	/// <param name="fromAddress">The sender's address. If not specified, the <see cref="IEmailSender"/> will use the default email address from its configuration settings.</param>
	/// <param name="attachments">The optional list of attachements for the email.</param>
	/// <param name="ccList">The list of email addresses to be added as CCs.</param>
	/// <param name="bccList">The list of email addresses to be added as BCCs.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A <see cref="Task"/> which completes when the email has been sent to the system which ultimately sends the email.</returns>
	/// <exception cref="UmbrellaWebException">Thrown if there is an error sending the email.</exception>
	protected async Task SendEmailAsync<TComponent, TModel>(TModel model, string email, string subject, IDictionary<string, object?>? parameters = null, string? fromAddress = null, IEnumerable<EmailAttachment>? attachments = null, IEnumerable<string>? ccList = null, IEnumerable<string>? bccList = null, CancellationToken cancellationToken = default)
		where TComponent : IModelRazorComponent<TModel>
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(model);

		try
		{
			string content = await ComponentToStringRenderer.RenderComponentToStringAsync<TComponent, TModel>(model, parameters, cancellationToken).ConfigureAwait(false);

			await SendEmailContentAsync(content, email, subject, fromAddress, attachments, ccList, bccList, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new
		{
			component = typeof(TComponent).Name,
			model,
			email,
			subject,
			fromAddress,
			parameters = SerializeParameters(parameters),
			attachments = SerializeAttachments(attachments),
			ccList = SerializeStringCollection(ccList),
			bccList = SerializeStringCollection(bccList)
		}))
		{
			throw new UmbrellaWebException($"There has been an error sending the '{subject}' email.", exc);
		}
	}
}
