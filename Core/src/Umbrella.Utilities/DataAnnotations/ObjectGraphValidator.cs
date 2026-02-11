
using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Logging;
using Umbrella.DataAnnotations.Helpers; // Reference AsyncValidator
using Umbrella.Utilities.DataAnnotations.Abstractions;
using Umbrella.Utilities.DataAnnotations.Options;
using Umbrella.Utilities.Exceptions;

namespace Umbrella.Utilities.DataAnnotations;

/// <summary>
/// A validator used to recursively validate an object graph which uses <see cref="ValidationAttribute"/>s.
/// </summary>
public class ObjectGraphValidator : IObjectGraphValidator
{
	/// <summary>Gets the logger.</summary>
	protected ILogger Logger { get; }

	/// <summary>Gets or sets the options.</summary>
	protected ObjectGraphValidatorOptions Options { get; set; }

	/// <summary>Gets the service provider used to resolve application services.</summary>
	protected IServiceProvider ServiceProvider { get; }

	/// <summary>
	/// Initializes a new instance of the ObjectGraphValidator class with the specified logger, options, and service
	/// provider.
	/// </summary>
	/// <param name="logger">The logger used to record diagnostic and validation information during object graph validation. Cannot be null.</param>
	/// <param name="options">The configuration options that control the behavior of the object graph validator. Cannot be null.</param>
	/// <param name="serviceProvider">The service provider used to resolve dependencies required during validation. Cannot be null.</param>
	public ObjectGraphValidator(
		ILogger<ObjectGraphValidator> logger,
		ObjectGraphValidatorOptions options,
		IServiceProvider serviceProvider)
	{
		Logger = logger;
		Options = options;
		ServiceProvider = serviceProvider;
	}

	/// <inheritdoc />
	public (bool isValid, IReadOnlyCollection<ObjectGraphValidationResult> results) TryValidateObject(object instance, ValidationContext? validationContext = null, bool validateAllProperties = false, IServiceProvider? serviceProvider = null)
	{
		Guard.IsNotNull(instance, nameof(instance));

		try
		{
			var visited = new HashSet<object>();
			var allResults = new List<ObjectGraphValidationResult>();

			void ValidateObject(object value, ValidationContext? parentContext, string? memberName, IServiceProvider? overrideProvider)
			{
				if (value is null)
					return;

				// Prevent infinite loops on cyclic graphs
				if (!visited.Add(value))
					return;

				// Build a fresh context that ALWAYS prefers the provided service provider override (falling back to root ServiceProvider).
				ValidationContext currentContext = CreateValidationContext(value, parentContext, memberName, overrideProvider ?? serviceProvider ?? ServiceProvider);

				// Collections
				if (value is IEnumerable enumerable and not string)
				{
					int index = 0;

					foreach (object? item in enumerable)
					{
						if (item is null)
						{
							index++;
							continue;
						}

						var type = item.GetType();
						if (ShouldSkipType(type))
						{
							index++;
							continue;
						}

						string itemMemberName = memberName is null
							? $"[{index}]"
							: $"{memberName}[{index}]";

						ValidateObject(item, currentContext, itemMemberName, overrideProvider);
						index++;
					}

					return;
				}

				// Validate current object
				var innerResults = new List<ValidationResult>();
				_ = Validator.TryValidateObject(
					value,
					currentContext,
					innerResults,
					validateAllProperties);

				if (innerResults.Count > 0)
					allResults.AddRange(innerResults.Select(r => new ObjectGraphValidationResult(r, value)));

				// Recurse into properties
				foreach (PropertyInfo pi in value.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
				{
					var propType = pi.PropertyType;
					if (ShouldSkipType(propType))
						continue;

					object? child = pi.GetValue(value);
					if (child is null)
						continue;

					ValidateObject(child, currentContext, pi.Name, overrideProvider);
				}
			}

			ValidateObject(instance, validationContext, validationContext?.MemberName, serviceProvider);

			return (allResults.Count == 0, allResults);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { validateAllProperties }))
		{
			throw new UmbrellaException("An error has been encountered whilst validating the object graph.", exc);
		}
	}

	/// <inheritdoc />
	public async Task<(bool isValid, IReadOnlyCollection<ObjectGraphValidationResult> results)> TryValidateObjectAsync(object instance, ValidationContext? validationContext = null, bool validateAllProperties = false, IServiceProvider? serviceProvider = null, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(instance, nameof(instance));

		try
		{
			var visited = new HashSet<object>();
			var allResults = new List<ObjectGraphValidationResult>();

			async Task ValidateObjectAsync(object value, ValidationContext? parentContext, string? memberName, IServiceProvider? overrideProvider)
			{
				cancellationToken.ThrowIfCancellationRequested();

				if (value is null)
					return;

				if (!visited.Add(value))
					return;

				var currentContext = CreateValidationContext(value, parentContext, memberName, overrideProvider ?? serviceProvider ?? ServiceProvider);

				if (value is IEnumerable enumerable and not string)
				{
					int index = 0;

					foreach (object? item in enumerable)
					{
						cancellationToken.ThrowIfCancellationRequested();

						if (item is null)
						{
							index++;
							continue;
						}

						var type = item.GetType();

						if (ShouldSkipType(type))
						{
							index++;
							continue;
						}

						string itemMemberName = memberName is null
							? $"[{index}]"
							: $"{memberName}[{index}]";

						await ValidateObjectAsync(item, currentContext, itemMemberName, overrideProvider).ConfigureAwait(false);
						index++;
					}

					return;
				}

				var innerResults = new List<ValidationResult>();

				_ = await AsyncValidator.TryValidateObjectAsync(
					value,
					currentContext,
					innerResults,
					validateAllProperties,
					cancellationToken).ConfigureAwait(false);

				if (innerResults.Count > 0)
					allResults.AddRange(innerResults.Select(r => new ObjectGraphValidationResult(r, value)));

				foreach (PropertyInfo pi in value.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
				{
					cancellationToken.ThrowIfCancellationRequested();

					var propType = pi.PropertyType;

					if (ShouldSkipType(propType))
						continue;

					object? child = pi.GetValue(value);

					if (child is null)
						continue;

					await ValidateObjectAsync(child, currentContext, pi.Name, overrideProvider).ConfigureAwait(false);
				}
			}

			await ValidateObjectAsync(instance, validationContext, validationContext?.MemberName, serviceProvider).ConfigureAwait(false);

			return (allResults.Count == 0, allResults);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { validateAllProperties }))
		{
			throw new UmbrellaException("An error has been encountered whilst validating the object graph asynchronously.", exc);
		}
	}

	private bool ShouldSkipType(Type type) =>
		type == typeof(string) ||
		type.IsPrimitive ||
		(Options.IgnorePropertyFilter?.Invoke(type) ?? false);

	private static ValidationContext CreateValidationContext(object instance, ValidationContext? parentContext, string? memberName, IServiceProvider provider)
	{
		// Copy items defensively to avoid unintended mutations propagating.
		IDictionary<object, object?>? items = parentContext?.Items;

		if (items is not null && items.Count > 0)
			items = new Dictionary<object, object?>(items);

		var ctx = new ValidationContext(instance, provider, items);

		if (!string.IsNullOrEmpty(memberName))
		{
			ctx.MemberName = memberName;
			ctx.DisplayName = memberName;
		}

		return ctx;
	}
}