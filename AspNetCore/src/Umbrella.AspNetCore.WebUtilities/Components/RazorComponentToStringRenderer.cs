using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Umbrella.AspNetCore.WebUtilities.Components.Abstractions;
using Umbrella.AspNetCore.WebUtilities.Components.Options;
using Umbrella.WebUtilities.Exceptions;

namespace Umbrella.AspNetCore.WebUtilities.Components;

/// <summary>
/// A utility used to render a component to a string.
/// </summary>
/// <seealso cref="IRazorComponentToStringRenderer" />
public class RazorComponentToStringRenderer : IRazorComponentToStringRenderer
{
	private readonly ILogger<RazorComponentToStringRenderer> _logger;
	private readonly IServiceProvider _serviceProvider;
	private readonly ILoggerFactory _loggerFactory;
	private readonly IHttpContextAccessor _httpContextAccessor;
	private readonly RazorComponentToStringRendererOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="RazorComponentToStringRenderer"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="serviceProvider">The service provider.</param>
	/// <param name="loggerFactory">The logger factory.</param>
	/// <param name="httpContextAccessor">The HTTP context accessor.</param>
	/// <param name="options">The options.</param>
	public RazorComponentToStringRenderer(
		ILogger<RazorComponentToStringRenderer> logger,
		IServiceProvider serviceProvider,
		ILoggerFactory loggerFactory,
		IHttpContextAccessor httpContextAccessor,
		RazorComponentToStringRendererOptions options)
	{
		_logger = logger;
		_serviceProvider = serviceProvider;
		_loggerFactory = loggerFactory;
		_httpContextAccessor = httpContextAccessor;
		_options = options;
	}

	/// <inheritdoc />
	public async Task<string> RenderComponentToStringAsync<TComponent>(IDictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default)
		where TComponent : IComponent
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			HttpContext? originalHttpContext = _httpContextAccessor.HttpContext;
			_httpContextAccessor.HttpContext ??= _options.CreateHttpContext(_serviceProvider);

			await using var htmlRenderer = new HtmlRenderer(_serviceProvider, _loggerFactory);
			try
			{
				return await htmlRenderer.Dispatcher.InvokeAsync(async () =>
				{
					cancellationToken.ThrowIfCancellationRequested();

					ParameterView parameterView = parameters is null
						? ParameterView.Empty
						: ParameterView.FromDictionary(parameters);

					var renderedComponent = await htmlRenderer.RenderComponentAsync<TComponent>(parameterView);

					cancellationToken.ThrowIfCancellationRequested();

					return renderedComponent.ToHtmlString();
				}).ConfigureAwait(false);
			}
			finally
			{
				_httpContextAccessor.HttpContext = originalHttpContext;
			}
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { component = typeof(TComponent).Name, parameters }))
		{
			throw new UmbrellaWebException("There has been a problem rendering the component.", exc);
		}
	}

	/// <inheritdoc />
	public async Task<string> RenderComponentToStringAsync<TComponent, TModel>(TModel model, IDictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default)
		where TComponent : IModelRazorComponent<TModel>
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			string modelPropertyName = ValidateModelProperty<TComponent, TModel>();
			Dictionary<string, object?> componentParameters = parameters is null
				? []
				: new Dictionary<string, object?>(parameters, StringComparer.Ordinal);

			if (componentParameters.ContainsKey(modelPropertyName))
				throw new InvalidOperationException($"A parameter named '{modelPropertyName}' has already been supplied.");

			componentParameters[modelPropertyName] = model;

			return await RenderComponentToStringAsync<TComponent>(componentParameters, cancellationToken).ConfigureAwait(false);
		}
		catch (UmbrellaWebException)
		{
			throw;
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { component = typeof(TComponent).Name, model, parameters }))
		{
			throw new UmbrellaWebException("There has been a problem rendering the component.", exc);
		}
	}

	private static string ValidateModelProperty<TComponent, TModel>()
		where TComponent : IModelRazorComponent<TModel>
	{
		const string modelPropertyName = nameof(IModelRazorComponent<TModel>.Model);

		Type componentType = typeof(TComponent);
		Type modelType = typeof(TModel);

		var modelProperty = componentType.GetProperty(modelPropertyName);

		if (modelProperty?.SetMethod?.IsPublic is not true)
			throw new InvalidOperationException($"The component '{componentType.FullName}' must define a public settable '{modelPropertyName}' property.");

		if (!modelProperty.PropertyType.IsAssignableFrom(modelType))
			throw new InvalidOperationException($"The '{modelPropertyName}' property on component '{componentType.FullName}' must be assignable from '{modelType.FullName}'.");

		if (!Attribute.IsDefined(modelProperty, typeof(ParameterAttribute), true))
			throw new InvalidOperationException($"The '{modelPropertyName}' property on component '{componentType.FullName}' must be decorated with the '{nameof(ParameterAttribute)}' attribute.");

		return modelPropertyName;
	}
}
