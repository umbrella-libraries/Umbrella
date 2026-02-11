


using System.Web.Mvc;

namespace Umbrella.Legacy.WebUtilities.Mvc.DataAnnotations;

/// <summary>
/// An extension of the <see cref="RemoteAttribute"/> to allow a WebAPI controller
/// to be used as the validation endpoint.
/// </summary>
/// <seealso cref="RemoteAttribute" />
public sealed class RemoteWebApiAttribute : RemoteAttribute
{
	/// <summary>
	/// Initializes a new instance of the <see cref="RemoteWebApiAttribute"/> class.
	/// </summary>
	/// <param name="controller">The controller.</param>
	/// <param name="routeName">Name of the route.</param>
#pragma warning disable CA1019 // Define accessors for attribute arguments
	public RemoteWebApiAttribute(string controller, string routeName = "DefaultApi")
#pragma warning restore CA1019 // Define accessors for attribute arguments
	{
		Controller = controller;
		RouteName = routeName;

		RouteData.Add("httproute", "");
		RouteData.Add("controller", controller);
	}

	/// <summary>
	/// The name of the controller.
	/// </summary>
	public string Controller { get; }
}