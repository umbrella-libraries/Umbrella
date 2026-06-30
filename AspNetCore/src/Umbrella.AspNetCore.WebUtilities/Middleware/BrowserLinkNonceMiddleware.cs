using System.Text;
using System.Text.RegularExpressions;
using CommunityToolkit.Diagnostics;
using Microsoft.AspNetCore.Http;
using Umbrella.WebUtilities.Security;

namespace Umbrella.AspNetCore.WebUtilities.Middleware;

/// <summary>
/// Middleware that injects a CSP nonce attribute onto Visual Studio BrowserLink and ASP.NET Core
/// hot-reload script tags that are automatically inserted into HTML responses during development.
/// Must be registered as the outermost middleware via <see cref="BrowserLinkNonceStartupFilter"/>.
/// </summary>
public partial class BrowserLinkNonceMiddleware
{
	#region Private Members
	[GeneratedRegex(@"(<script\b[^>]*\bsrc=""/_vs/browserLink""[^>]*?)(>)", RegexOptions.IgnoreCase)]
	private static partial Regex BrowserLinkPattern();

	[GeneratedRegex(@"(<script\b[^>]*\bsrc=""/_framework/aspnetcore-browser-refresh\.js""[^>]*?)(>)", RegexOptions.IgnoreCase)]
	private static partial Regex BrowserRefreshPattern();

	private readonly RequestDelegate _next;
	#endregion

	#region Constructors
	/// <summary>
	/// Initializes a new instance of the <see cref="BrowserLinkNonceMiddleware"/> class.
	/// </summary>
	/// <param name="next">The next middleware in the pipeline.</param>
	public BrowserLinkNonceMiddleware(RequestDelegate next)
	{
		_next = next;
	}
	#endregion

	#region Middleware Members
	/// <summary>
	/// Invokes the middleware for the specified <see cref="HttpContext"/>.
	/// </summary>
	/// <param name="context">The <see cref="HttpContext"/>.</param>
	/// <param name="nonceContext">The <see cref="NonceContext"/> for the current request.</param>
	/// <returns>An awaitable <see cref="Task"/>.</returns>
	public async Task InvokeAsync(HttpContext context, NonceContext nonceContext)
	{
		Guard.IsNotNull(context, nameof(context));
		Guard.IsNotNull(nonceContext, nameof(nonceContext));

		Stream originalBody = context.Response.Body;

		using var buffer = new MemoryStream();
		context.Response.Body = buffer;

		try
		{
			await _next(context);
		}
		finally
		{
			context.Response.Body = originalBody;
		}

		// Nonce is set by security middleware during _next — read it after the inner pipeline completes.
		string? nonce = nonceContext.Current;

		string? contentType = context.Response.ContentType;
		bool isHtml = !string.IsNullOrEmpty(contentType) &&
					  contentType.Contains("text/html", StringComparison.OrdinalIgnoreCase);

		if (string.IsNullOrEmpty(nonce) || !isHtml)
		{
			buffer.Position = 0;
			await buffer.CopyToAsync(originalBody);
			return;
		}

		buffer.Position = 0;
		string html = Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);

		string replacement = $"$1 nonce=\"{nonce}\"$2";
		html = BrowserLinkPattern().Replace(html, replacement);
		html = BrowserRefreshPattern().Replace(html, replacement);

		byte[] modifiedBytes = Encoding.UTF8.GetBytes(html);
		context.Response.ContentLength = modifiedBytes.Length;
		await originalBody.WriteAsync(modifiedBytes);
	}
	#endregion
}
