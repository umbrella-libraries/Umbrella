using CommunityToolkit.Diagnostics;
using Microsoft.AspNetCore.Components.Authorization;
using Umbrella.Utilities.Options.Abstractions;

namespace Umbrella.AspNetCore.Blazor.Components.Breadcrumb.Options;

/// <summary>
/// Options for use with the <see cref="UmbrellaBreadcrumb"/> component.
/// </summary>
public class UmbrellaBreadcrumbOptions : ISanitizableUmbrellaOptions, IValidatableUmbrellaOptions
{
	/// <summary>
	/// Gets or sets the name of the root breadcrumb item.
	/// Used when <see cref="RootNameFactory"/> is <see langword="null"/>.
	/// </summary>
	public string RootName { get; set; } = "Home";

	/// <summary>
	/// Gets or sets the URL of the root breadcrumb item.
	/// Used when <see cref="RootPathFactory"/> is <see langword="null"/>.
	/// </summary>
	public string RootPath { get; set; } = "/";

	/// <summary>
	/// Gets or sets a factory that returns the root breadcrumb item name at render time.
	/// When set, takes precedence over <see cref="RootName"/>. The <see cref="IServiceProvider"/>
	/// argument is the scoped provider for the current request or circuit, giving access to
	/// services such as <c>IHttpContextAccessor"</c> (SSR) or
	/// <see cref="AuthenticationStateProvider"/> (interactive).
	/// </summary>
	public Func<IServiceProvider, string>? RootNameFactory { get; set; }

	/// <summary>
	/// Gets or sets a factory that returns the root breadcrumb item URL at render time.
	/// When set, takes precedence over <see cref="RootPath"/>. The <see cref="IServiceProvider"/>
	/// argument is the scoped provider for the current request or circuit, giving access to
	/// services such as <c>IHttpContextAccessor"</c> (SSR) or
	/// <see cref="AuthenticationStateProvider"/> (interactive).
	/// </summary>
	public Func<IServiceProvider, string>? RootPathFactory { get; set; }

	/// <inheritdoc />
	public void Sanitize()
	{
		RootName = RootName?.Trim()!;
		RootPath = RootPath?.Trim()!;
	}

	/// <inheritdoc />
	public void Validate()
	{
		if (RootNameFactory is null)
			Guard.IsNotNullOrEmpty(RootName);

		if (RootPathFactory is null)
			Guard.IsNotNullOrEmpty(RootPath);
	}
}
