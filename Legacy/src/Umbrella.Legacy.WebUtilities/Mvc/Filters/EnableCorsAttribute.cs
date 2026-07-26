using System.Globalization;
using System.Web.Mvc;

namespace Umbrella.Legacy.WebUtilities.Mvc.Filters;

/// <summary>
/// An action filter that outputs CORS headers to the HTTP response.
/// </summary>
/// <seealso cref="ActionFilterAttribute" />
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class EnableCorsAttribute : ActionFilterAttribute
{
	/// <summary>
	/// Gets or sets a value indicating whether to output the "Access-Control-Allow-Credentials" header with a value of <see langword="true"/>.
	/// </summary>
	public bool AllowCredentials { get; set; }

	/// <summary>
	/// Gets or sets the "Access-Control-Allow-Origin" header value.
	/// </summary>
	public string? AllowOrigin { get; set; }

	/// <summary>
	/// Gets or sets the "Access-Control-Allow-Headers" header value.
	/// </summary>
	public string? AllowHeaders { get; set; }

	/// <summary>
	/// Gets or sets the "Access-Control-Allow-Methods" header value.
	/// </summary>
	public string? AllowMethods { get; set; }

	/// <summary>
	/// Gets or sets the "Access-Control-Expose-Headers" header value.
	/// </summary>
	public string? ExposeHeaders { get; set; }

	/// <summary>
	/// Gets or sets the value of the "Access-Control-Max-Age" header.
	/// </summary>
	public int MaxAgeSeconds { get; set; }

	/// <inheritdoc />
	public override void OnActionExecuting(ActionExecutingContext filterContext)
	{
		if (filterContext is null)
			throw new ArgumentNullException(nameof(filterContext));

		var response = filterContext.RequestContext.HttpContext.Response;

		void TryAddStringHeader(string name, string? value)
		{
			if (!string.IsNullOrWhiteSpace(value))
				response.AddHeader(name, value);
		}

		if (AllowCredentials)
			response.AddHeader("Access-Control-Allow-Credentials", "true");

		if (MaxAgeSeconds > 0)
			response.AddHeader("Access-Control-Max-Age", MaxAgeSeconds.ToString(CultureInfo.InvariantCulture));

		TryAddStringHeader("Access-Control-Allow-Origin", AllowOrigin);
		TryAddStringHeader("Access-Control-Allow-Headers", AllowHeaders);
		TryAddStringHeader("Access-Control-Allow-Methods", AllowMethods);
		TryAddStringHeader("Access-Control-Expose-Headers", ExposeHeaders);

		base.OnActionExecuting(filterContext);
	}
}
