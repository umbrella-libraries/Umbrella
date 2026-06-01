namespace Umbrella.DataAccess.Analyzers.Test;

public class UDA005_RepositoryIQueryableAnalyzerTests : AnalyzerTestBase<RepositoryIQueryableAnalyzer>
{
	// Stubs occupy lines 1-13; appended test code starts at line 14.
	// IQueryable<T> is stubbed here since System.Linq.Expressions is not in the default test references.
	private const string RepositoryStubs = @"namespace Umbrella.DataAccess.EntityFrameworkCore
{
    public abstract class ReadOnlyGenericDbRepository<TEntity> { }
    public abstract class GenericDbRepository<TEntity> : ReadOnlyGenericDbRepository<TEntity> { }
}

namespace System.Linq
{
    public interface IQueryable<T> { }
}

namespace System.Collections.Generic { }
namespace System.Threading.Tasks { }
";

	[Fact]
	public async Task PublicMethod_ReturningIQueryable_ReportsDiagnosticUDA005()
	{
		const string source = RepositoryStubs + @"using System.Linq;
using Umbrella.DataAccess.EntityFrameworkCore;

namespace TestApp
{
    public class ThingRepository : GenericDbRepository<object>
    {
        public IQueryable<object> GetItems() => null!;
    }
}";
		// Line 14: using System.Linq;
		// Line 15: using Umbrella.DataAccess.EntityFrameworkCore;
		// Line 16: (blank)
		// Line 17: namespace TestApp
		// Line 18: {
		// Line 19:     public class ThingRepository ...
		// Line 20:     {
		// Line 21:         public IQueryable<object> GetItems() => null!;
		//          "        public IQueryable<object> " = 8+7+18 = 33 → col 34
		await VerifyAnalyzerAsync(source, Diagnostic(RepositoryIQueryableAnalyzer.IQueryableForbiddenRule, 21, 35, "GetItems"));
	}

	[Fact]
	public async Task PublicMethod_ReturningTaskOfIQueryable_ReportsDiagnosticUDA005()
	{
		const string source = RepositoryStubs + @"using System.Linq;
using System.Threading.Tasks;
using Umbrella.DataAccess.EntityFrameworkCore;

namespace TestApp
{
    public class ThingRepository : GenericDbRepository<object>
    {
        public Task<IQueryable<object>> GetItemsAsync() => null!;
    }
}";
		// Line 14: using System.Linq;
		// Line 15: using System.Threading.Tasks;
		// Line 16: using Umbrella.DataAccess.EntityFrameworkCore;
		// Line 17: (blank)
		// Line 18: namespace TestApp
		// Line 19: {
		// Line 20:     public class ThingRepository ...
		// Line 21:     {
		// Line 22:         public Task<IQueryable<object>> GetItemsAsync() => null!;
		//          "        public Task<IQueryable<object>> " = 8+7+24 = 39 → col 40
		await VerifyAnalyzerAsync(source, Diagnostic(RepositoryIQueryableAnalyzer.IQueryableForbiddenRule, 22, 41, "GetItemsAsync"));
	}

	[Fact]
	public async Task OverrideMethod_ReturningIQueryable_ReportsDiagnosticUDA005()
	{
		// Override exemption does not apply to UDA005 — IQueryable is unconditionally forbidden.
		const string source = RepositoryStubs + @"using System.Linq;
using Umbrella.DataAccess.EntityFrameworkCore;

namespace TestApp
{
    public class ThingRepository : GenericDbRepository<object>
    {
        public override IQueryable<object> GetItems() => null!;
    }
}";
		// Line 21:         public override IQueryable<object> GetItems() => null!;
		//          "        public override IQueryable<object> " = 8+7+9+18 = 42 → col 43
		await VerifyAnalyzerAsync(source, Diagnostic(RepositoryIQueryableAnalyzer.IQueryableForbiddenRule, 21, 44, "GetItems"));
	}

	[Fact]
	public async Task PublicMethod_ReturningIReadOnlyCollection_NoDiagnostic()
	{
		const string source = RepositoryStubs + @"using System.Collections.Generic;
using System.Threading.Tasks;
using Umbrella.DataAccess.EntityFrameworkCore;

namespace TestApp
{
    public class ThingRepository : GenericDbRepository<object>
    {
        public Task<IReadOnlyCollection<object>> FindAllByStatusAsync() => Task.FromResult<IReadOnlyCollection<object>>(new List<object>());
    }
}";
		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task NonPublicMethod_ReturningIQueryable_NoDiagnostic()
	{
		const string source = RepositoryStubs + @"using System.Linq;
using Umbrella.DataAccess.EntityFrameworkCore;

namespace TestApp
{
    public class ThingRepository : GenericDbRepository<object>
    {
        protected IQueryable<object> GetItems() => null!;
    }
}";
		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task NonRepositoryClass_ReturningIQueryable_NoDiagnostic()
	{
		const string source = RepositoryStubs + @"using System.Linq;

namespace TestApp
{
    public class ThingService
    {
        public IQueryable<object> GetItems() => null!;
    }
}";
		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task ReadOnlyRepo_ReturningIQueryable_ReportsDiagnosticUDA005()
	{
		const string source = RepositoryStubs + @"using System.Linq;
using Umbrella.DataAccess.EntityFrameworkCore;

namespace TestApp
{
    public class ThingReadRepository : ReadOnlyGenericDbRepository<object>
    {
        public IQueryable<object> GetItems() => null!;
    }
}";
		// Line 21:         public IQueryable<object> GetItems() => null!;   ← col 34
		await VerifyAnalyzerAsync(source, Diagnostic(RepositoryIQueryableAnalyzer.IQueryableForbiddenRule, 21, 35, "GetItems"));
	}
}
