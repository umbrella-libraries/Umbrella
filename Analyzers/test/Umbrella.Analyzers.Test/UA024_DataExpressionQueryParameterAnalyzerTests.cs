namespace Umbrella.Analyzers.Test;

public class UA024_DataExpressionQueryParameterAnalyzerTests : AnalyzerTestBase<DataExpressionQueryParameterAnalyzer>
{
	private const string Stubs = """
		using System;
		using System.Collections.Generic;
		using System.Linq.Expressions;
		using System.Threading.Tasks;

		namespace Microsoft.AspNetCore.Mvc
		{
		    public abstract class ControllerBase { }

		    public abstract class Controller : ControllerBase { }

		    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
		    public sealed class NonActionAttribute : Attribute { }

		    [AttributeUsage(AttributeTargets.Parameter)]
		    public sealed class FromQueryAttribute : Attribute { }

		    [AttributeUsage(AttributeTargets.Parameter)]
		    public sealed class FromBodyAttribute : Attribute { }
		}

		namespace Umbrella.Utilities.Data.Sorting
		{
		    public enum SortDirection { Ascending, Descending }

		    public readonly struct SortExpression<TItem>
		    {
		        public Expression<Func<TItem, object>>? Expression { get; }
		        public SortDirection Direction { get; }
		    }

		    public class SortExpressionDescriptor
		    {
		        public string? MemberPath { get; set; }
		        public SortDirection Direction { get; set; }
		    }
		}

		namespace Umbrella.Utilities.Data.Filtering
		{
		    public enum FilterType { Contains, Equal }

		    public readonly struct FilterExpression<TItem>
		    {
		        public Expression<Func<TItem, object>>? Expression { get; }
		        public FilterType Type { get; }
		    }

		    public class FilterExpressionDescriptor
		    {
		        public string? MemberPath { get; set; }
		        public FilterType Type { get; set; }
		    }
		}

		namespace TestApp
		{
		    public class Widget
		    {
		        public int Id { get; set; }
		    }
		}

		""";

	[Fact]
	public async Task SingleFilterExpressionParameter_ShouldReportDiagnostic()
	{
		const string source = Stubs + """
			namespace TestApp
			{
			    using Microsoft.AspNetCore.Mvc;
			    using Umbrella.Utilities.Data.Filtering;

			    public class WidgetController : ControllerBase
			    {
			        public Task<int> SearchAsync([FromQuery] FilterExpression<Widget> filter) => Task.FromResult(0);
			    }
			}
			""";

		await VerifyAnalyzerAsync(
			source,
			ExpectedAt(source, "FilterExpression<Widget> filter", "filter", "filter", "SearchAsync", "FilterExpression<Widget>"));
	}

	[Fact]
	public async Task SingleSortExpressionParameter_ShouldReportDiagnostic()
	{
		const string source = Stubs + """
			namespace TestApp
			{
			    using Microsoft.AspNetCore.Mvc;
			    using Umbrella.Utilities.Data.Sorting;

			    public class WidgetController : ControllerBase
			    {
			        public Task<int> SearchAsync([FromQuery] SortExpression<Widget> sorter) => Task.FromResult(0);
			    }
			}
			""";

		await VerifyAnalyzerAsync(
			source,
			ExpectedAt(source, "SortExpression<Widget> sorter", "sorter", "sorter", "SearchAsync", "SortExpression<Widget>"));
	}

	[Fact]
	public async Task NullableSingleDataExpressionParameter_ShouldReportDiagnostic()
	{
		const string source = Stubs + """
			namespace TestApp
			{
			    using Microsoft.AspNetCore.Mvc;
			    using Umbrella.Utilities.Data.Filtering;

			    public class WidgetController : ControllerBase
			    {
			        public Task<int> SearchAsync([FromQuery] FilterExpression<Widget>? filter = null) => Task.FromResult(0);
			    }
			}
			""";

		await VerifyAnalyzerAsync(
			source,
			ExpectedAt(source, "FilterExpression<Widget>? filter", "filter", "filter", "SearchAsync", "FilterExpression<Widget>"));
	}

	[Fact]
	public async Task SingleDataExpressionBoundFromBody_ShouldReportDiagnostic()
	{
		// The Data Expression model binders read a JSON document from a single query string value, so a body-bound
		// single expression is broken in its own right. The binding source is deliberately not considered.
		const string source = Stubs + """
			namespace TestApp
			{
			    using Microsoft.AspNetCore.Mvc;
			    using Umbrella.Utilities.Data.Filtering;

			    public class WidgetController : ControllerBase
			    {
			        public Task<int> SearchAsync([FromBody] FilterExpression<Widget> filter) => Task.FromResult(0);
			    }
			}
			""";

		await VerifyAnalyzerAsync(
			source,
			ExpectedAt(source, "FilterExpression<Widget> filter", "filter", "filter", "SearchAsync", "FilterExpression<Widget>"));
	}

	[Fact]
	public async Task SingleDataExpressionWithoutBindingAttribute_ShouldReportDiagnostic()
	{
		const string source = Stubs + """
			namespace TestApp
			{
			    using Microsoft.AspNetCore.Mvc;
			    using Umbrella.Utilities.Data.Filtering;

			    public class WidgetController : ControllerBase
			    {
			        public Task<int> SearchAsync(FilterExpression<Widget> filter) => Task.FromResult(0);
			    }
			}
			""";

		await VerifyAnalyzerAsync(
			source,
			ExpectedAt(source, "FilterExpression<Widget> filter", "filter", "filter", "SearchAsync", "FilterExpression<Widget>"));
	}

	[Fact]
	public async Task MultipleSingleDataExpressionParameters_ShouldReportDiagnosticForEach()
	{
		const string source = Stubs + """
			namespace TestApp
			{
			    using Microsoft.AspNetCore.Mvc;
			    using Umbrella.Utilities.Data.Filtering;
			    using Umbrella.Utilities.Data.Sorting;

			    public class WidgetController : ControllerBase
			    {
			        public Task<int> SearchAsync(
			            [FromQuery] SortExpression<Widget>? sorter,
			            [FromQuery] FilterExpression<Widget>? filter) => Task.FromResult(0);
			    }
			}
			""";

		await VerifyAnalyzerAsync(
			source,
			ExpectedAt(source, "SortExpression<Widget>? sorter", "sorter", "sorter", "SearchAsync", "SortExpression<Widget>"),
			ExpectedAt(source, "FilterExpression<Widget>? filter", "filter", "filter", "SearchAsync", "FilterExpression<Widget>"));
	}

	[Fact]
	public async Task DerivedControllerAction_ShouldReportDiagnostic()
	{
		const string source = Stubs + """
			namespace TestApp
			{
			    using Microsoft.AspNetCore.Mvc;
			    using Umbrella.Utilities.Data.Filtering;

			    public abstract class AppControllerBase : Controller { }

			    public class WidgetController : AppControllerBase
			    {
			        public Task<int> SearchAsync([FromQuery] FilterExpression<Widget> filter) => Task.FromResult(0);
			    }
			}
			""";

		await VerifyAnalyzerAsync(
			source,
			ExpectedAt(source, "FilterExpression<Widget> filter", "filter", "filter", "SearchAsync", "FilterExpression<Widget>"));
	}

	[Fact]
	public async Task SingleDataExpressionOnGenericControllerBase_ShouldReportDiagnostic()
	{
		// Generic controller bases are the Umbrella pattern, so the open generic form must be caught too.
		const string source = Stubs + """
			namespace TestApp
			{
			    using Microsoft.AspNetCore.Mvc;
			    using Umbrella.Utilities.Data.Filtering;

			    public abstract class GenericController<TEntity> : ControllerBase
			    {
			        public virtual Task<int> SearchAsync([FromQuery] FilterExpression<TEntity> filter) => Task.FromResult(0);
			    }
			}
			""";

		await VerifyAnalyzerAsync(
			source,
			ExpectedAt(source, "FilterExpression<TEntity> filter", "filter", "filter", "SearchAsync", "FilterExpression<TEntity>"));
	}

	[Fact]
	public async Task ArraysOfDataExpressionsOnGenericControllerBase_ShouldNotReportDiagnostic()
	{
		const string source = Stubs + """
			namespace TestApp
			{
			    using Microsoft.AspNetCore.Mvc;
			    using Umbrella.Utilities.Data.Filtering;
			    using Umbrella.Utilities.Data.Sorting;

			    public abstract class GenericController<TEntity> : ControllerBase
			    {
			        public virtual Task<int> SearchSlimAsync(
			            int pageNumber,
			            int pageSize,
			            [FromQuery] SortExpression<TEntity>[]? sorters = null,
			            [FromQuery] FilterExpression<TEntity>[]? filters = null) => Task.FromResult(0);
			    }

			    public class WidgetController : GenericController<Widget> { }
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task ArrayOfDataExpressions_ShouldNotReportDiagnostic()
	{
		// The form UmbrellaGenericRepositoryApiController.SearchSlimAsync uses. Collections are not flattened.
		const string source = Stubs + """
			namespace TestApp
			{
			    using Microsoft.AspNetCore.Mvc;
			    using Umbrella.Utilities.Data.Filtering;
			    using Umbrella.Utilities.Data.Sorting;

			    public class WidgetController : ControllerBase
			    {
			        public Task<int> SearchSlimAsync(
			            int pageNumber,
			            int pageSize,
			            [FromQuery] SortExpression<Widget>[]? sorters = null,
			            [FromQuery] FilterExpression<Widget>[]? filters = null) => Task.FromResult(0);
			    }
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task EnumerableOfDataExpressions_ShouldNotReportDiagnostic()
	{
		const string source = Stubs + """
			namespace TestApp
			{
			    using Microsoft.AspNetCore.Mvc;
			    using Umbrella.Utilities.Data.Filtering;
			    using Umbrella.Utilities.Data.Sorting;

			    public class WidgetController : ControllerBase
			    {
			        public Task<int> SearchAsync(
			            [FromQuery] IEnumerable<SortExpression<Widget>>? sorters = null,
			            [FromQuery] IEnumerable<FilterExpression<Widget>>? filters = null) => Task.FromResult(0);
			    }
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task DescriptorTypes_ShouldNotReportDiagnostic()
	{
		// The descriptors carry no expression tree, so ApiExplorer can flatten them harmlessly and the
		// OpenAPI operation transformer collapses them back into a single JSON parameter.
		const string source = Stubs + """
			namespace TestApp
			{
			    using Microsoft.AspNetCore.Mvc;
			    using Umbrella.Utilities.Data.Filtering;
			    using Umbrella.Utilities.Data.Sorting;

			    public class WidgetController : ControllerBase
			    {
			        public Task<int> SearchAsync(
			            [FromQuery] FilterExpressionDescriptor? filter = null,
			            [FromQuery] IEnumerable<SortExpressionDescriptor>? sorters = null) => Task.FromResult(0);
			    }
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task NonActionAttribute_ShouldNotReportDiagnostic()
	{
		const string source = Stubs + """
			namespace TestApp
			{
			    using Microsoft.AspNetCore.Mvc;
			    using Umbrella.Utilities.Data.Filtering;

			    public class WidgetController : ControllerBase
			    {
			        [NonAction]
			        public Task<int> SearchAsync([FromQuery] FilterExpression<Widget> filter) => Task.FromResult(0);
			    }
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task NonActionAttributeOnOverriddenBaseMethod_ShouldNotReportDiagnostic()
	{
		const string source = Stubs + """
			namespace TestApp
			{
			    using Microsoft.AspNetCore.Mvc;
			    using Umbrella.Utilities.Data.Filtering;

			    public abstract class AppControllerBase : ControllerBase
			    {
			        [NonAction]
			        public virtual Task<int> SearchAsync(FilterExpression<Widget> filter) => Task.FromResult(0);
			    }

			    public class WidgetController : AppControllerBase
			    {
			        public override Task<int> SearchAsync(FilterExpression<Widget> filter) => Task.FromResult(0);
			    }
			}
			""";

		// The base declaration is itself excluded, so neither declaration is reported.
		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task NonPublicControllerMethod_ShouldNotReportDiagnostic()
	{
		// UmbrellaDataAccessApiController.ReadAllAsync is a protected helper, not an action.
		const string source = Stubs + """
			namespace TestApp
			{
			    using Microsoft.AspNetCore.Mvc;
			    using Umbrella.Utilities.Data.Filtering;
			    using Umbrella.Utilities.Data.Sorting;

			    public abstract class AppControllerBase : ControllerBase
			    {
			        protected virtual Task<int> ReadAllAsync(
			            SortExpression<Widget> sorter,
			            FilterExpression<Widget> filter) => Task.FromResult(0);

			        private Task<int> LoadAsync(FilterExpression<Widget> filter) => Task.FromResult(0);
			    }
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task StaticControllerMethod_ShouldNotReportDiagnostic()
	{
		const string source = Stubs + """
			namespace TestApp
			{
			    using Microsoft.AspNetCore.Mvc;
			    using Umbrella.Utilities.Data.Filtering;

			    public class WidgetController : ControllerBase
			    {
			        public static Task<int> MapAsync(FilterExpression<Widget> filter) => Task.FromResult(0);
			    }
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task NonControllerType_ShouldNotReportDiagnostic()
	{
		// Services and repositories legitimately accept a single data expression.
		const string source = Stubs + """
			namespace TestApp
			{
			    using Umbrella.Utilities.Data.Filtering;
			    using Umbrella.Utilities.Data.Sorting;

			    public class WidgetService
			    {
			        public Task<int> SearchAsync(
			            SortExpression<Widget> sorter,
			            FilterExpression<Widget> filter) => Task.FromResult(0);
			    }
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task ControllerWithNoDataExpressionParameters_ShouldNotReportDiagnostic()
	{
		const string source = Stubs + """
			namespace TestApp
			{
			    using Microsoft.AspNetCore.Mvc;

			    public class WidgetController : ControllerBase
			    {
			        public Task<int> GetAsync(int id, string? search = null) => Task.FromResult(id);
			    }
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	private static ExpectedDiagnostic ExpectedAt(
		string source,
		string declaration,
		string anchor,
		string parameterName,
		string methodName,
		string expressionTypeName)
	{
		int declarationIndex = source.IndexOf(declaration, StringComparison.Ordinal);
		int anchorIndex = declarationIndex + declaration.IndexOf(anchor, StringComparison.Ordinal);
		string precedingText = source[..anchorIndex];
		int line = precedingText.Count(static character => character == '\n') + 1;
		int column = anchorIndex - precedingText.LastIndexOf('\n');

		return Diagnostic(
			DataExpressionQueryParameterAnalyzer.Rule,
			line,
			column,
			parameterName,
			methodName,
			expressionTypeName);
	}
}
