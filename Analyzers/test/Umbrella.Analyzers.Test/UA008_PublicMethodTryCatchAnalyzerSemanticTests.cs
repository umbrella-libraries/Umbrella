namespace Umbrella.Analyzers.Test;

public class UA008_PublicMethodTryCatchAnalyzerSemanticTests : AnalyzerTestBase<PublicMethodTryCatchAnalyzer>
{
	private const string UmbrellaLoggingExtension = """
		namespace Microsoft.Extensions.Logging
		{
			public static class ILoggerExtensions
			{
				public static bool WriteError(this ILogger logger, System.Exception exc, object state = null) => true;
			}
		}
		""";

	private static readonly Microsoft.CodeAnalysis.MetadataReference _loggingReference =
		Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(typeof(Microsoft.Extensions.Logging.ILogger).Assembly.Location);

	[Fact]
	public async Task StaticMethodWithILogger_ShouldNotTriggerDiagnostic()
	{
		const string source = """
			using Microsoft.Extensions.Logging;
			public class C
			{
				private readonly ILogger _logger;
				public C(ILogger logger) { _logger = logger; }
				public static void M() { }
			}
			""";

		await VerifyNoDiagnosticsAsync(source, [_loggingReference]);
	}

	[Fact]
	public async Task UserDefinedILogger_ShouldNotTriggerDiagnostic()
	{
		const string source = """
			public interface ILogger { }
			public class C
			{
				private readonly ILogger _logger;
				public C(ILogger logger) { _logger = logger; }
				public void M() { }
			}
			""";

		await VerifyNoDiagnosticsAsync(source, [_loggingReference]);
	}

	[Fact]
	public async Task InterfaceImplementationWithILoggerAndNoTry_ShouldTriggerDiagnostic()
	{
		const string source = """
			using Microsoft.Extensions.Logging;
			public interface IService { void M(int value); }
			public class C : IService
			{
				private readonly ILogger _logger;
				public C(ILogger logger) { _logger = logger; }
				public void M(int value) { }
			}
			""";
		var expected = Diagnostic(PublicMethodTryCatchAnalyzer.Rule, 7, 14, "M");

		await VerifyAnalyzerAsync(source, [_loggingReference], expected);
	}

	[Fact]
	public async Task OverrideWithInheritedILoggerAndNoTry_ShouldTriggerDiagnostic()
	{
		const string source = """
			using Microsoft.Extensions.Logging;
			public abstract class Base
			{
				protected Base(ILogger logger) { Logger = logger; }
				protected ILogger Logger { get; }
				public abstract void M();
			}
			public class C : Base
			{
				public C(ILogger logger) : base(logger) { }
				public override void M() { }
			}
			""";
		var expected = Diagnostic(PublicMethodTryCatchAnalyzer.Rule, 11, 23, "M");

		await VerifyAnalyzerAsync(source, [_loggingReference], expected);
	}

	[Fact]
	public async Task ValidationPreambleAndWriteErrorState_ShouldNotTriggerDiagnostic()
	{
		string source = """
			
			using System;
			using System.Threading;
			using Microsoft.Extensions.Logging;
			public class C
			{
				private readonly ILogger _logger;
				public C(ILogger logger) { _logger = logger; }
				public void M(int appUserId, CancellationToken cancellationToken = default)
				{
					cancellationToken.ThrowIfCancellationRequested();
					ArgumentOutOfRangeException.ThrowIfNegative(appUserId);
					try { }
					catch (Exception exc) when (_logger.WriteError(exc, new { appUserId })) { throw; }
				}
			}
			""" + UmbrellaLoggingExtension;

		await VerifyNoDiagnosticsAsync(source, [_loggingReference]);
	}

	[Fact]
	public async Task MeaningfulParameterWithoutLoggedState_ShouldTriggerDiagnostic()
	{
		string source = """
			
			using System;
			using Microsoft.Extensions.Logging;
			public class C
			{
				private readonly ILogger _logger;
				public C(ILogger logger) { _logger = logger; }
				public void M(int appUserId)
				{
					try { }
					catch (Exception exc) when (_logger.WriteError(exc)) { throw; }
				}
			}
			""" + UmbrellaLoggingExtension;
		var expected = Diagnostic(PublicMethodTryCatchAnalyzer.Rule, 8, 14, "M");

		await VerifyAnalyzerAsync(source, [_loggingReference], expected);
	}

	[Fact]
	public async Task CancellationTokenOnlyWithoutLoggedState_ShouldNotTriggerDiagnostic()
	{
		string source = """
			
			using System;
			using System.Threading;
			using Microsoft.Extensions.Logging;
			public class C
			{
				private readonly ILogger _logger;
				public C(ILogger logger) { _logger = logger; }
				public void M(CancellationToken cancellationToken = default)
				{
					cancellationToken.ThrowIfCancellationRequested();
					try { }
					catch (Exception exc) when (_logger.WriteError(exc)) { throw; }
				}
			}
			""" + UmbrellaLoggingExtension;

		await VerifyNoDiagnosticsAsync(source, [_loggingReference]);
	}

	[Fact]
	public async Task OperationalStatementBeforeTry_ShouldTriggerDiagnostic()
	{
		string source = """
			
			using System;
			using Microsoft.Extensions.Logging;
			public class C
			{
				private readonly ILogger _logger;
				public C(ILogger logger) { _logger = logger; }
				public void M()
				{
					int value = 1;
					try { }
					catch (Exception exc) when (_logger.WriteError(exc)) { throw; }
				}
			}
			""" + UmbrellaLoggingExtension;
		var expected = Diagnostic(PublicMethodTryCatchAnalyzer.Rule, 8, 14, "M");

		await VerifyAnalyzerAsync(source, [_loggingReference], expected);
	}

	[Fact]
	public async Task OperationalStatementAfterTry_ShouldTriggerDiagnostic()
	{
		string source = """
			
			using System;
			using Microsoft.Extensions.Logging;
			public class C
			{
				private readonly ILogger _logger;
				public C(ILogger logger) { _logger = logger; }
				public void M()
				{
					try { }
					catch (Exception exc) when (_logger.WriteError(exc)) { throw; }
					int value = 1;
				}
			}
			""" + UmbrellaLoggingExtension;
		var expected = Diagnostic(PublicMethodTryCatchAnalyzer.Rule, 8, 14, "M");

		await VerifyAnalyzerAsync(source, [_loggingReference], expected);
	}

	[Fact]
	public async Task OneOfMultipleCatchBlocksWithoutLogging_ShouldTriggerDiagnostic()
	{
		string source = """
			
			using System;
			using Microsoft.Extensions.Logging;
			public class C
			{
				private readonly ILogger _logger;
				public C(ILogger logger) { _logger = logger; }
				public void M()
				{
					try { }
					catch (ArgumentException exc) { throw; }
					catch (Exception exc) when (_logger.WriteError(exc)) { throw; }
				}
			}
			""" + UmbrellaLoggingExtension;
		var expected = Diagnostic(PublicMethodTryCatchAnalyzer.Rule, 8, 14, "M");

		await VerifyAnalyzerAsync(source, [_loggingReference], expected);
	}

	[Fact]
	public async Task StructuredLogErrorWithState_ShouldNotTriggerDiagnostic()
	{
		const string source = """
			using System;
			using Microsoft.Extensions.Logging;
			public class C
			{
				private readonly ILogger _logger;
				public C(ILogger logger) { _logger = logger; }
				public void M(int appUserId)
				{
					try { }
					catch (Exception exc)
					{
						_logger.LogError(exc, "Failed for user {AppUserId}", appUserId);
						throw;
					}
				}
			}
			""";

		await VerifyNoDiagnosticsAsync(source, [_loggingReference]);
	}
}
