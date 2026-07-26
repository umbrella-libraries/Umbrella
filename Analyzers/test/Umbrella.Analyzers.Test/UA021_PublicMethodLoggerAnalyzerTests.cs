using Microsoft.CodeAnalysis;

namespace Umbrella.Analyzers.Test;

public class UA021_PublicMethodLoggerAnalyzerTests : AnalyzerTestBase<PublicMethodLoggerAnalyzer>
{
	private static readonly MetadataReference _loggingReference =
		MetadataReference.CreateFromFile(typeof(Microsoft.Extensions.Logging.ILogger).Assembly.Location);

	[Fact]
	public async Task OperationalPublicInstanceMethodWithoutLogger_ShouldTriggerDiagnostic()
	{
		const string source = """
			public class TestClass
			{
				private int _value;
				public void Update(int value) { _value = value; }
			}
			""";

		await VerifyAnalyzerAsync(
			source,
			[_loggingReference],
			ExpectedAt(source, "class TestClass", "TestClass", "Update"));
	}

	[Fact]
	public async Task MultipleOperationalMethods_ShouldReportOncePerType()
	{
		const string source = """
			public class TestClass
			{
				private int _value;
				public void Update(int value) { _value = value; }
				public int Read() => _value;
			}
			""";

		await VerifyAnalyzerAsync(
			source,
			[_loggingReference],
			ExpectedLocationAt(source, "class TestClass", "TestClass"));
	}

	[Theory]
	[InlineData("ILogger")]
	[InlineData("ILogger<TestClass>")]
	public async Task LoggerField_ShouldSatisfyRule(string loggerType)
	{
		string source = $$"""
			using Microsoft.Extensions.Logging;
			public class TestClass
			{
				private readonly {{loggerType}} _logger;
				private int _value;
				public TestClass({{loggerType}} logger) { _logger = logger; }
				public void Update(int value) { _value = value; }
			}
			""";

		await VerifyNoDiagnosticsAsync(source, [_loggingReference]);
	}

	[Fact]
	public async Task LoggerProperty_ShouldSatisfyRule()
	{
		const string source = """
			using Microsoft.Extensions.Logging;
			public class TestClass
			{
				private ILogger Logger { get; }
				private int _value;
				public TestClass(ILogger logger) { Logger = logger; }
				public void Update(int value) { _value = value; }
			}
			""";

		await VerifyNoDiagnosticsAsync(source, [_loggingReference]);
	}

	[Fact]
	public async Task PrimaryConstructorLoggerParameter_ShouldSatisfyRule()
	{
		const string source = """
			using Microsoft.Extensions.Logging;
			public class TestClass(ILogger<TestClass> logger)
			{
				private int _value;
				public void Update(int value) { _value = value; }
			}
			""";

		await VerifyNoDiagnosticsAsync(source, [_loggingReference]);
	}

	[Fact]
	public async Task ProtectedInheritedLogger_ShouldSatisfyRule()
	{
		const string source = """
			using Microsoft.Extensions.Logging;
			public abstract class Base
			{
				protected ILogger Logger { get; }
				protected Base(ILogger logger) { Logger = logger; }
			}
			public class TestClass : Base
			{
				private int _value;
				public TestClass(ILogger logger) : base(logger) { }
				public void Update(int value) { _value = value; }
			}
			""";

		await VerifyNoDiagnosticsAsync(source, [_loggingReference]);
	}

	[Fact]
	public async Task PrivateInheritedLogger_ShouldNotSatisfyRule()
	{
		const string source = """
			using Microsoft.Extensions.Logging;
			public abstract class Base
			{
				private readonly ILogger _logger;
				protected Base(ILogger logger) { _logger = logger; }
			}
			public class TestClass : Base
			{
				private int _value;
				public TestClass(ILogger logger) : base(logger) { }
				public void Update(int value) { _value = value; }
			}
			""";

		await VerifyAnalyzerAsync(
			source,
			[_loggingReference],
			ExpectedAt(source, "class TestClass", "TestClass", "Update"));
	}

	[Fact]
	public async Task LoggerConstructorParameterThatIsNotStored_ShouldTriggerDiagnostic()
	{
		const string source = """
			using Microsoft.Extensions.Logging;
			public class TestClass
			{
				private int _value;
				public TestClass(ILogger logger) { }
				public void Update(int value) { _value = value; }
			}
			""";

		await VerifyAnalyzerAsync(
			source,
			[_loggingReference],
			ExpectedAt(source, "class TestClass", "TestClass", "Update"));
	}

	[Fact]
	public async Task OperationalRecordMethodWithoutLogger_ShouldTriggerDiagnostic()
	{
		const string source = """
			public record TestRecord
			{
				private int _value;
				public void Update(int value) { _value = value; }
			}
			""";

		await VerifyAnalyzerAsync(
			source,
			[_loggingReference],
			ExpectedAt(source, "record TestRecord", "TestRecord", "Update"));
	}

	[Fact]
	public async Task UserDefinedILogger_ShouldNotSatisfyRule()
	{
		const string source = """
			public interface ILogger { }
			public class TestClass
			{
				private readonly ILogger _logger;
				private int _value;
				public TestClass(ILogger logger) { _logger = logger; }
				public void Update(int value) { _value = value; }
			}
			""";

		await VerifyAnalyzerAsync(
			source,
			[_loggingReference],
			ExpectedAt(source, "class TestClass", "TestClass", "Update"));
	}

	[Fact]
	public async Task ILoggerFactoryAlone_ShouldNotSatisfyRule()
	{
		const string source = """
			using Microsoft.Extensions.Logging;
			public class TestClass
			{
				private readonly ILoggerFactory _loggerFactory;
				private int _value;
				public TestClass(ILoggerFactory loggerFactory) { _loggerFactory = loggerFactory; }
				public void Update(int value) { _value = value; }
			}
			""";

		await VerifyAnalyzerAsync(
			source,
			[_loggingReference],
			ExpectedAt(source, "class TestClass", "TestClass", "Update"));
	}

	[Fact]
	public async Task StaticAndNonPublicMethods_ShouldNotTriggerDiagnostic()
	{
		const string source = """
			public class TestClass
			{
				private int _value;
				public static void PublicStatic() { System.GC.Collect(); }
				internal void Internal() { _value++; }
				protected void Protected() { _value++; }
				private void Private() { _value++; }
			}
			""";

		await VerifyNoDiagnosticsAsync(source, [_loggingReference]);
	}

	[Fact]
	public async Task AbstractMethod_ShouldNotTriggerDiagnostic()
	{
		const string source = "public abstract class TestClass { public abstract void Update(int value); }";
		await VerifyNoDiagnosticsAsync(source, [_loggingReference]);
	}

	[Theory]
	[InlineData("public void M() { }")]
	[InlineData("public void M(string value) { System.ArgumentNullException.ThrowIfNull(value); }")]
	[InlineData("public int M() => 42;")]
	[InlineData("public int M() => default;")]
	[InlineData("public System.Threading.Tasks.Task M() => System.Threading.Tasks.Task.CompletedTask;")]
	public async Task TrivialPublicMethod_ShouldNotTriggerDiagnostic(string method)
	{
		string source = $"public class TestClass {{ {method} }}";
		await VerifyNoDiagnosticsAsync(source, [_loggingReference]);
	}

	[Fact]
	public async Task DirectBaseForwarder_ShouldNotTriggerDiagnostic()
	{
		const string source = """
			public class Base
			{
				public virtual int Update(int value) => default;
			}
			public class TestClass : Base
			{
				public override int Update(int value) => base.Update(value);
			}
			""";

		await VerifyNoDiagnosticsAsync(source, [_loggingReference]);
	}

	[Fact]
	public async Task UmbrellaTrimmableImplementation_ShouldNotTriggerDiagnostic()
	{
		const string source = """
			namespace Umbrella.Utilities.Text
			{
				public interface IUmbrellaTrimmable
				{
					void TrimAllStringProperties();
				}
			}
			public class TestModel : Umbrella.Utilities.Text.IUmbrellaTrimmable
			{
				public string? Value { get; set; }
				public void TrimAllStringProperties() { Value = Value?.Trim(); }
			}
			""";

		await VerifyNoDiagnosticsAsync(source, [_loggingReference]);
	}

	[Fact]
	public async Task EntityOperationalMethod_ShouldNotTriggerDiagnostic()
	{
		const string source = """
			namespace Umbrella.DataAccess.Abstractions
			{
				public interface IEntity<TKey> { }
			}
			public class TestEntity : Umbrella.DataAccess.Abstractions.IEntity<int>
			{
				public string GetDisplayText(int value) => value.ToString();
			}
			""";

		await VerifyNoDiagnosticsAsync(source, [_loggingReference]);
	}

	[Fact]
	public async Task NonActionMethod_ShouldNotTriggerDiagnostic()
	{
		const string source = """
			namespace Microsoft.AspNetCore.Mvc
			{
				public sealed class NonActionAttribute : System.Attribute { }
			}
			public class TestClass
			{
				private int _value;
				[Microsoft.AspNetCore.Mvc.NonAction]
				public void Update(int value) { _value = value; }
			}
			""";

		await VerifyNoDiagnosticsAsync(source, [_loggingReference]);
	}

	[Fact]
	public async Task MiddlewareEntryPoint_ShouldNotTriggerDiagnostic()
	{
		const string source = """
			namespace Microsoft.AspNetCore.Http
			{
				public delegate System.Threading.Tasks.Task RequestDelegate(object context);
			}
			public class TestMiddleware
			{
				private readonly Microsoft.AspNetCore.Http.RequestDelegate _next;
				public TestMiddleware(Microsoft.AspNetCore.Http.RequestDelegate next) { _next = next; }
				public System.Threading.Tasks.Task InvokeAsync(object context) => _next(context);
			}
			""";

		await VerifyNoDiagnosticsAsync(source, [_loggingReference]);
	}

	[Fact]
	public async Task DisposalImplementation_ShouldNotTriggerDiagnostic()
	{
		const string source = """
			public class TestClass : System.IDisposable
			{
				private object? _resource = new object();
				public void Dispose() { _resource = null; }
			}
			""";

		await VerifyNoDiagnosticsAsync(source, [_loggingReference]);
	}

	[Theory]
	[InlineData("Xunit", "FactAttribute")]
	[InlineData("NUnit.Framework", "TestAttribute")]
	[InlineData("Microsoft.VisualStudio.TestTools.UnitTesting", "TestMethodAttribute")]
	public async Task RecognizedTestEntryPoint_ShouldNotTriggerDiagnostic(
		string attributeNamespace,
		string attributeName)
	{
		ArgumentNullException.ThrowIfNull(attributeNamespace);
		ArgumentNullException.ThrowIfNull(attributeName);

		string source = $$"""
			namespace {{attributeNamespace}}
			{
				public sealed class {{attributeName}} : System.Attribute { }
			}
			public class TestClass
			{
				private int _value;
				[{{attributeNamespace}}.{{attributeName.Replace("Attribute", "", StringComparison.Ordinal)}}]
				public void Execute() { _value++; }
			}
			""";

		await VerifyNoDiagnosticsAsync(source, [_loggingReference]);
	}

	[Fact]
	public async Task BlazorComponentOperationalHandlerWithoutLogger_ShouldTriggerDiagnostic()
	{
		const string source = """
			namespace Microsoft.AspNetCore.Components
			{
				public abstract class ComponentBase { }
			}
			public class TestComponent : Microsoft.AspNetCore.Components.ComponentBase
			{
				private int _clickCount;
				public void HandleClick() { _clickCount++; }
			}
			""";

		await VerifyAnalyzerAsync(
			source,
			[_loggingReference],
			ExpectedAt(source, "class TestComponent", "TestComponent", "HandleClick"));
	}

	[Fact]
	public async Task BlazorComponentWithInheritedLogger_ShouldSatisfyRule()
	{
		const string source = """
			using Microsoft.Extensions.Logging;
			namespace Microsoft.AspNetCore.Components
			{
				public abstract class ComponentBase
				{
					protected ILogger Logger { get; }
					protected ComponentBase(ILogger logger) { Logger = logger; }
				}
			}
			public class TestComponent : Microsoft.AspNetCore.Components.ComponentBase
			{
				private int _clickCount;
				public TestComponent(ILogger logger) : base(logger) { }
				public void HandleClick() { _clickCount++; }
			}
			""";

		await VerifyNoDiagnosticsAsync(source, [_loggingReference]);
	}

	[Fact]
	public async Task GeneratedType_ShouldNotTriggerDiagnostic()
	{
		const string source = """
			// <auto-generated/>
			public class TestClass
			{
				private int _value;
				public void Update(int value) { _value = value; }
			}
			""";

		await VerifyNoDiagnosticsAsync(source, [_loggingReference]);
	}

	private static ExpectedDiagnostic ExpectedAt(
		string source,
		string declaration,
		string typeName,
		string methodName)
	{
		ExpectedDiagnostic location = ExpectedLocationAt(source, declaration, typeName);
		return Diagnostic(PublicMethodLoggerAnalyzer.Rule, location.Line, location.Column, typeName, methodName);
	}

	private static ExpectedDiagnostic ExpectedLocationAt(
		string source,
		string declaration,
		string typeName)
	{
		int declarationIndex = source.IndexOf(declaration, StringComparison.Ordinal);
		int typeIndex = declarationIndex + declaration.IndexOf(typeName, StringComparison.Ordinal);
		string precedingText = source[..typeIndex];
		int line = precedingText.Count(static character => character == '\n') + 1;
		int column = typeIndex - precedingText.LastIndexOf('\n');

		return Diagnostic(PublicMethodLoggerAnalyzer.Rule, line, column);
	}
}
