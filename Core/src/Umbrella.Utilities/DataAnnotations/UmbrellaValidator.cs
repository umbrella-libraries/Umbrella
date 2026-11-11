using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using Umbrella.Utilities.DataAnnotations.Abstractions;
using Umbrella.Utilities.DataAnnotations.Enumerations;
using Umbrella.Utilities.Exceptions;

namespace Umbrella.Utilities.DataAnnotations;

/// <summary>
/// A validation utility used to perform validation on an object instance which uses <see cref="ValidationAttribute"/>s.
/// </summary>
public class UmbrellaValidator : IUmbrellaValidator
{
	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaValidator"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="objectGraphValidator">The object graph validator.</param>
	/// <param name="serviceProvider">The service provider.</param>
	public UmbrellaValidator(
		ILogger<UmbrellaValidator> logger,
		IObjectGraphValidator objectGraphValidator,
		IServiceProvider serviceProvider)
	{
		Logger = logger;
		ObjectGraphValidator = objectGraphValidator;
		ServiceProvider = serviceProvider;
	}

	/// <summary>
	/// Gets the logger.
	/// </summary>
	protected ILogger Logger { get; }

	/// <summary>
	/// Gets the graph validator.
	/// </summary>
	protected IObjectGraphValidator ObjectGraphValidator { get; }

	/// <summary>
	/// Gets the service provider used to resolve application services.
	/// </summary>
	public IServiceProvider ServiceProvider { get; }

	/// <inheritdoc />
	public async Task<(bool isValid, IReadOnlyCollection<ValidationResult> results)> ValidateItemAsync(object item, ValidationType validationType)
	{
		if (validationType is ValidationType.None)
			return (true, Array.Empty<ValidationResult>());

		var ctx = new ValidationContext(item, ServiceProvider, null);

		if (validationType is ValidationType.Deep)
			return await ObjectGraphValidator.TryValidateObjectAsync(item, ctx, true, serviceProvider: ServiceProvider);

		if (validationType is ValidationType.Shallow)
		{
			var lstResult = new List<ValidationResult>();

#if NET11_0_OR_GREATER
			bool valid = await Validator.TryValidateObjectAsync(item, ctx, lstResult, true).ConfigureAwait(false);
#else
			bool valid = Validator.TryValidateObject(item, ctx, lstResult, true);
#endif

			return (valid, lstResult);
		}

		throw new UmbrellaException("Unknown validation type.");
	}
}