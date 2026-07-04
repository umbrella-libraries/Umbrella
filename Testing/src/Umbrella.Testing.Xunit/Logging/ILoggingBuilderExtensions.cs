using Microsoft.Extensions.DependencyInjection;
using Umbrella.Testing.Xunit.Logging;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.Logging;

/// <summary>
/// Extension methods for registering xUnit v3 logging providers.
/// </summary>
public static class ILoggingBuilderExtensions
{
	extension(ILoggingBuilder builder)
	{
		/// <summary>
		/// Registers a logger provider that writes log messages to the current xUnit v3 test output helper.
		/// </summary>
		/// <returns>The supplied <paramref name="builder"/>.</returns>
		public ILoggingBuilder AddXUnitTestOutputHelperLogging()
		{
			ArgumentNullException.ThrowIfNull(builder);

			_ = builder.Services.AddSingleton<ILoggerProvider, XUnitTestOutputHelperLoggerProvider>();

			return builder;
		}
	}
}