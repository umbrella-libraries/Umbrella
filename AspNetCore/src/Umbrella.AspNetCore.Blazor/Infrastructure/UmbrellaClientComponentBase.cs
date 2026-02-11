using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Claims;
using CommunityToolkit.Diagnostics;
using Microsoft.AspNetCore.Components.Forms;
using Umbrella.AppFramework.Security.Abstractions;
using Umbrella.AppFramework.Services.Constants;
using Umbrella.AppFramework.Shared.Constants;
using Umbrella.AppFramework.Shared.Security;
using Umbrella.AppFramework.Shared.Security.Extensions;
using Umbrella.AspNetCore.Blazor.Components.Dialog.Abstractions;
using Umbrella.AspNetCore.Blazor.Enumerations;
using Umbrella.AspNetCore.Shared.Components.Abstractions;
using Umbrella.Utilities.Http;
using Umbrella.Utilities.Primitives.Abstractions;

namespace Umbrella.AspNetCore.Blazor.Infrastructure;

/// <summary>
/// A base component to be used with Blazor components which contain commonly used functionality.
/// </summary>
/// <seealso cref="ComponentBase" />
/// <seealso cref="IAsyncDisposable"/>
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)]
public abstract class UmbrellaClientComponentBase : UmbrellaComponentBase
{
	/// <summary>
	/// Gets the dialog utility.
	/// </summary>
	[Inject]
	protected IUmbrellaDialogService DialogUtility { get; private set; } = null!;

	/// <summary>
	/// Gets the authentication helper.
	/// </summary>
	// TODO: Need to get rid of this somehow.
	[Inject]
	protected IAppAuthHelper AuthHelper { get; private set; } = null!;

	/// <summary>
	/// Gets or sets the current layout state. The initial state is <see cref="LayoutState.Loading"/>.
	/// </summary>
	protected LayoutState CurrentState { get; set; } = LayoutState.Loading;

	/// <summary>
	/// Shows the problem details error message. If this does not exist, the error message defaults to <see cref="DialogDefaults.UnknownErrorMessage"/>.
	/// </summary>
	/// <param name="problemDetails">The problem details.</param>
	/// <param name="title">The title.</param>
	protected ValueTask ShowProblemDetailsErrorMessageAsync(HttpProblemDetails? problemDetails, string title = "Error")
		=> DialogUtility.ShowProblemDetailsErrorMessageAsync(problemDetails, title);

	/// <summary>
	/// Shows a friendly error message for the specified <paramref name="operationResult"/>.
	/// </summary>
	/// <param name="operationResult">The erroneous operation result.</param>
	/// <param name="title">The title.</param>
	/// <returns>A task that completes when the dialog has been actioned.</returns>
	protected ValueTask ShowOperationResultErrorMessageAsync(IOperationResult? operationResult, string title = "Error")
		=> DialogUtility.ShowOperationResultErrorMessageAsync(operationResult, title);

	/// <summary>
	/// Gets the value as a string suitable for use as a value with <![CDATA[<option>]]> elements of a <see cref="InputSelect{TValue}"/> component.
	/// </summary>
	/// <typeparam name="TValue">The type of the value.</typeparam>
	/// <param name="value">The value.</param>
	/// <returns>The value converted to a string if not null; otherwise "-1"</returns>
	[Obsolete("The InputSelect element support binding using integers now so this workaround can be removed.")]
	protected static string GetValueAsInputSelectString<TValue>(TValue? value) where TValue : struct => value?.ToString() ?? "-1";

	/// <summary>
	/// Sets a property value on the specified model using an income string value used with an <![CDATA[<option>]]> element of a <see cref="InputSelect{TValue}"/> component
	/// using the specified <paramref name="assignor"/> delegate.
	/// </summary>
	/// <typeparam name="TValue">The type of the value.</typeparam>
	/// <param name="model">The model.</param>
	/// <param name="value">The value.</param>
	/// <param name="assignor">The assignor.</param>
	/// <remarks>The string <paramref name="value"/> is converted to <typeparamref name="TValue"/> before assignment to the property on the specified <paramref name="model"/>.</remarks>
	[Obsolete("The InputSelect element support binding using integers now so this workaround can be removed.")]
	protected static void SetValueFromInputSelectString<TValue>(object? model, string? value, Action<TValue?> assignor)
		where TValue : struct
	{
		Guard.IsNotNull(assignor);

		if (model is null)
			return;

		object? convertedValue = null;

		if (typeof(TValue).IsEnum)
		{
			if (Enum.TryParse<TValue>(value, out var enumValue) && Enum.IsDefined(typeof(TValue), enumValue))
			{
				convertedValue = enumValue;
			}
		}
		else
		{
			convertedValue = value == "-1" ? null : Convert.ChangeType(value, typeof(TValue), CultureInfo.InvariantCulture);
		}

		if (convertedValue is null or default(object?))
		{
			assignor(null);
		}
		else
		{
			assignor((TValue)convertedValue);
		}
	}

	/// <summary>
	/// Appends the a file access token as a query string parameter with the key <see cref="AppQueryStringKeys.FileAccessToken" /> using the value stored
	/// using the <see cref="UmbrellaAppClaimType.FileAccessToken "/> claim, if it exists on the current <see cref="ClaimsPrincipal"/>.
	/// </summary>
	/// <param name="uri">The URI to append the token to.</param>
	/// <returns>The URI with the appended token if the <see cref="UmbrellaAppClaimType.FileAccessToken" /> claim exists; otherwise the value of the <paramref name="uri"/> parameter.</returns>
	public async Task<string> AppendFileAccessTokenAsync(string uri)
	{
		var authState = await GetClaimsPrincipalAsync();
		string? accessToken = authState.GetFileAccessToken();

		string updatedUrl = string.IsNullOrWhiteSpace(accessToken) ? uri : QueryHelpers.AddQueryString(uri, AppQueryStringKeys.FileAccessToken, accessToken);

		return updatedUrl;
	}

	/// <summary>
	/// Reloads the component primarily in response to an error during the initial loading phase.
	/// </summary>
	/// <remarks>Defaults to <see cref="ComponentBase.OnInitializedAsync"/></remarks>
	/// <returns>An awaitable Task that completed when this operation has completed.</returns>
	protected virtual Task ReloadAsync() => OnInitializedAsync();

	/// <summary>
	/// Shows a generic validation error message.
	/// </summary>
	/// <returns>An awaitable Task that completed when this operation has completed.</returns>
	protected virtual async Task ShowValidationErrorMessageAsync() => await DialogUtility.ShowDangerMessageAsync("Please correct all validation errors.");
}