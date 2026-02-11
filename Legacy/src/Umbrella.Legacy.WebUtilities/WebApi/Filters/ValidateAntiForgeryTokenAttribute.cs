


using System.Web.Http.Filters;

namespace Umbrella.Legacy.WebUtilities.WebApi.Filters;

/// <summary>
/// An implementation of the ValidateAntiForgeryToken Action Filter attribute used in MVC for use in Web API.
/// This works by looking for the token inside the HTTP Headers with the key "X-Request-Verification-Token"
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = true)]
public sealed class ValidateAntiForgeryTokenAttribute : ActionFilterAttribute
{
	/// <inheritdoc/>
	public override void OnActionExecuting(System.Web.Http.Controllers.HttpActionContext actionContext)
	{
		string cookieToken = "";
		string formToken = "";

		if (actionContext.Request.Headers.TryGetValues("X-Request-Verification-Token", out IEnumerable<string> tokenHeaders))
		{
			string[] tokens = tokenHeaders.First().Split(':');
			if (tokens.Length == 2)
			{
				cookieToken = tokens[0].Trim();
				formToken = tokens[1].Trim();
			}
		}

		System.Web.Helpers.AntiForgery.Validate(cookieToken, formToken);

		base.OnActionExecuting(actionContext);
	}
}