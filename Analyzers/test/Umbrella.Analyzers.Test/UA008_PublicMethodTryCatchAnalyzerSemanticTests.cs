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
				public void M(int value) { System.Console.WriteLine(value); }
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
				public override void M() { System.Console.WriteLine(); }
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
					int value = Environment.TickCount;
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
	public async Task SpecificCatchWithoutLoggingAndBroadCatchWithLogging_ShouldNotTriggerDiagnostic()
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

		await VerifyNoDiagnosticsAsync(source, [_loggingReference]);
	}

	[Fact]
	public async Task SafeLocalStateDeclarationBeforeTry_ShouldNotTriggerDiagnostic()
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
					int? appUserId = null;
					try { appUserId = Environment.TickCount; }
					catch (Exception exc) when (_logger.WriteError(exc, new { appUserId })) { throw; }
				}
			}
			""" + UmbrellaLoggingExtension;

		await VerifyNoDiagnosticsAsync(source, [_loggingReference]);
	}

	[Fact]
	public async Task DirectBaseForwarders_ShouldNotTriggerDiagnostic()
	{
		const string source = """
			using System;
			using Microsoft.Extensions.Logging;
			public abstract class Base
			{
				protected Base(ILogger logger) { Logger = logger; }
				protected ILogger Logger { get; }
				protected int ForwardCore(int value) => value;
				protected int QueryCore(Func<int> valueFactory) => valueFactory();
			}
			public sealed class C : Base
			{
				public C(ILogger logger) : base(logger) { }
				public int ExpressionForwarder(int value) => base.ForwardCore(value);
				public int LambdaForwarder(int value) => base.QueryCore(() => value);
				public int BlockForwarder(int value)
				{
					System.ArgumentOutOfRangeException.ThrowIfNegative(value);
					return base.ForwardCore(value);
				}
			}
			""";

		await VerifyNoDiagnosticsAsync(source, [_loggingReference]);
	}

	[Fact]
	public async Task ForwarderToMethodOnSameType_ShouldTriggerDiagnostic()
	{
		const string source = """
			using Microsoft.Extensions.Logging;
			public sealed class C
			{
				private readonly ILogger _logger;
				public C(ILogger logger) { _logger = logger; }
				private int ForwardCore(int value) => value;
				public int M(int value) => ForwardCore(value);
			}
			""";

		await VerifyAnalyzerAsync(source, [_loggingReference], ExpectedAt(source, "M"));
	}

	[Fact]
	public async Task NonActionMethod_ShouldNotTriggerDiagnostic()
	{
		const string source = """
			using System;
			using Microsoft.Extensions.Logging;
			namespace Microsoft.AspNetCore.Mvc
			{
				public sealed class NonActionAttribute : Attribute { }
			}
			public sealed class C
			{
				private readonly ILogger _logger;
				public C(ILogger logger) { _logger = logger; }
				[Microsoft.AspNetCore.Mvc.NonAction]
				public void M() { Console.WriteLine(); }
			}
			""";

		await VerifyNoDiagnosticsAsync(source, [_loggingReference]);
	}

	[Fact]
	public async Task MiddlewareEntryPoint_ShouldNotTriggerDiagnostic()
	{
		const string source = """
			using System.Threading.Tasks;
			using Microsoft.Extensions.Logging;
			namespace Microsoft.AspNetCore.Http
			{
				public sealed class HttpContext { }
				public delegate Task RequestDelegate(HttpContext context);
			}
			public sealed class SecurityMiddleware
			{
				private readonly ILogger _logger;
				private readonly Microsoft.AspNetCore.Http.RequestDelegate _next;
				public SecurityMiddleware(ILogger logger, Microsoft.AspNetCore.Http.RequestDelegate next)
				{
					_logger = logger;
					_next = next;
				}
				public Task InvokeAsync(Microsoft.AspNetCore.Http.HttpContext context) => _next(context);
			}
			""";

		await VerifyNoDiagnosticsAsync(source, [_loggingReference]);
	}

	[Fact]
	public async Task DisposalImplementations_ShouldNotTriggerDiagnostic()
	{
		const string source = """
			using System;
			using System.Threading.Tasks;
			using Microsoft.Extensions.Logging;
			public sealed class C : IDisposable, IAsyncDisposable
			{
				private readonly ILogger _logger;
				public C(ILogger logger) { _logger = logger; }
				public void Dispose() { Console.WriteLine(); }
				public ValueTask DisposeAsync() { Console.WriteLine(); return ValueTask.CompletedTask; }
			}
			""";

		await VerifyNoDiagnosticsAsync(source, [_loggingReference]);
	}

	[Fact]
	public async Task ValidationAndCompletedTaskNoOp_ShouldNotTriggerDiagnostic()
	{
		const string source = """
			using System;
			using System.Threading.Tasks;
			using Microsoft.Extensions.Logging;
			public sealed class C
			{
				private readonly ILogger _logger;
				public C(ILogger logger) { _logger = logger; }
				public Task M(string value)
				{
					ArgumentNullException.ThrowIfNull(value);
					return Task.CompletedTask;
				}
			}
			""";

		await VerifyNoDiagnosticsAsync(source, [_loggingReference]);
	}

	[Fact]
	public async Task BlazorComponentEventHandlerIsNotGloballyExempt()
	{
		const string source = """
			using Microsoft.Extensions.Logging;
			namespace Microsoft.AspNetCore.Components
			{
				public abstract class ComponentBase { }
			}
			public sealed class C : Microsoft.AspNetCore.Components.ComponentBase
			{
				private readonly ILogger _logger;
				public C(ILogger logger) { _logger = logger; }
				public void OnClick() { System.Console.WriteLine(); }
			}
			""";

		await VerifyAnalyzerAsync(source, [_loggingReference], ExpectedAt(source, "OnClick"));
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

	private static ExpectedDiagnostic ExpectedAt(string source, string methodName)
	{
		int index = source.IndexOf(methodName + "(", StringComparison.Ordinal);
		int line = source.Take(index).Count(x => x == '\n') + 1;
		int lineStart = source.LastIndexOf('\n', index);
		int column = index - lineStart;

		return Diagnostic(PublicMethodTryCatchAnalyzer.Rule, line, column, methodName);
	}
}
