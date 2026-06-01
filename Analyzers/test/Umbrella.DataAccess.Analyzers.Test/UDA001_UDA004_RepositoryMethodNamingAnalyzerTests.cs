namespace Umbrella.DataAccess.Analyzers.Test;

public class UDA001_UDA004_RepositoryMethodNamingAnalyzerTests : AnalyzerTestBase<RepositoryMethodNamingAnalyzer>
{
	// Stubs occupy lines 1-11; appended test code starts at line 12.
	private const string RepositoryStubs = @"namespace Umbrella.DataAccess.EntityFrameworkCore
{
    public abstract class ReadOnlyGenericDbRepository<TEntity> { }
    public abstract class GenericDbRepository<TEntity> : ReadOnlyGenericDbRepository<TEntity> { }
}

namespace Umbrella.DataAccess.Abstractions
{
    public class PaginatedResultModel<T> { }
    public interface IOperationResult<T> { }
}
";

	// ── UDA001 ──────────────────────────────────────────────────────────────

	[Fact]
	public async Task SingleItemReturn_NamedGetBy_ReportsDiagnosticUDA001()
	{
		const string source = RepositoryStubs + @"using System.Threading.Tasks;
using Umbrella.DataAccess.EntityFrameworkCore;

namespace TestApp
{
    public class ThingRepository : GenericDbRepository<object>
    {
        public Task<object?> GetByIdAsync(int id) => Task.FromResult<object?>(null);
    }
}";
		// Line 12: using System.Threading.Tasks;
		// Line 13: using Umbrella.DataAccess.EntityFrameworkCore;
		// Line 14: (blank)
		// Line 15: namespace TestApp
		// Line 16: {
		// Line 17:     public class ThingRepository ...
		// Line 18:     {
		// Line 19:         public Task<object?> GetByIdAsync(...)    ← identifier at col 30
		await VerifyAnalyzerAsync(source, Diagnostic(RepositoryMethodNamingAnalyzer.FindByRule, 19, 30, "GetByIdAsync"));
	}

	[Fact]
	public async Task SingleItemReturn_CorrectlyNamedFindBy_NoDiagnostic()
	{
		const string source = RepositoryStubs + @"using System.Threading.Tasks;
using Umbrella.DataAccess.EntityFrameworkCore;

namespace TestApp
{
    public class ThingRepository : GenericDbRepository<object>
    {
        public Task<object?> FindByIdAsync(int id) => Task.FromResult<object?>(null);
    }
}";
		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task IOperationResultReturn_NamedGetBy_ReportsDiagnosticUDA001()
	{
		const string source = RepositoryStubs + @"using System.Threading.Tasks;
using Umbrella.DataAccess.EntityFrameworkCore;
using Umbrella.DataAccess.Abstractions;

namespace TestApp
{
    public class ThingRepository : GenericDbRepository<object>
    {
        public Task<IOperationResult<object>> GetByIdAsync(int id) => Task.FromResult<IOperationResult<object>>(null!);
    }
}";
		// Line 12: using System.Threading.Tasks;
		// Line 13: using Umbrella.DataAccess.EntityFrameworkCore;
		// Line 14: using Umbrella.DataAccess.Abstractions;
		// Line 15: (blank)
		// Line 16: namespace TestApp
		// Line 17: {
		// Line 18:     public class ThingRepository ...
		// Line 19:     {
		// Line 20:         public Task<IOperationResult<object>> GetByIdAsync(...)
		//          "        public Task<IOperationResult<object>> " = 8+7+30 = 45 → col 46
		await VerifyAnalyzerAsync(source, Diagnostic(RepositoryMethodNamingAnalyzer.FindByRule, 20, 47, "GetByIdAsync"));
	}

	[Fact]
	public async Task ReadOnlyRepo_SingleItemReturn_NamedGetBy_ReportsDiagnosticUDA001()
	{
		const string source = RepositoryStubs + @"using System.Threading.Tasks;
using Umbrella.DataAccess.EntityFrameworkCore;

namespace TestApp
{
    public class ThingReadRepository : ReadOnlyGenericDbRepository<object>
    {
        public Task<object?> GetByStatusAsync(string status) => Task.FromResult<object?>(null);
    }
}";
		// Line 19:     public Task<object?> GetByStatusAsync(...)   ← col 30
		await VerifyAnalyzerAsync(source, Diagnostic(RepositoryMethodNamingAnalyzer.FindByRule, 19, 30, "GetByStatusAsync"));
	}

	[Fact]
	public async Task AbstractBaseRepo_SingleItemReturn_NamedGetBy_ReportsDiagnosticUDA001()
	{
		const string source = RepositoryStubs + @"using System.Threading.Tasks;
using Umbrella.DataAccess.EntityFrameworkCore;

namespace TestApp
{
    public abstract class ProjectBaseRepository : GenericDbRepository<object>
    {
        public abstract Task<object?> GetByIdAsync(int id);
    }
}";
		// Line 19:         public abstract Task<object?> GetByIdAsync(int id);
		//          "        public abstract Task<object?> " = 8+7+9+14 = 38 → col 39
		await VerifyAnalyzerAsync(source, Diagnostic(RepositoryMethodNamingAnalyzer.FindByRule, 19, 39, "GetByIdAsync"));
	}

	[Fact]
	public async Task ConcreteRepoInheritingAbstractBase_SingleItemReturn_NamedGetBy_ReportsDiagnosticUDA001()
	{
		const string source = RepositoryStubs + @"using System.Threading.Tasks;
using Umbrella.DataAccess.EntityFrameworkCore;

namespace TestApp
{
    public abstract class ProjectBaseRepository : GenericDbRepository<object>
    {
    }

    public class ConcreteRepository : ProjectBaseRepository
    {
        public Task<object?> GetByIdAsync(int id) => Task.FromResult<object?>(null);
    }
}";
		// Line 12: using System.Threading.Tasks;
		// Line 13: using Umbrella.DataAccess.EntityFrameworkCore;
		// Line 14: (blank)
		// Line 15: namespace TestApp
		// Line 16: {
		// Line 17:     public abstract class ProjectBaseRepository ...
		// Line 18:     {
		// Line 19:     }
		// Line 20:     (blank)
		// Line 21:     public class ConcreteRepository ...
		// Line 22:     {
		// Line 23:         public Task<object?> GetByIdAsync(...)    ← col 30
		await VerifyAnalyzerAsync(source, Diagnostic(RepositoryMethodNamingAnalyzer.FindByRule, 23, 30, "GetByIdAsync"));
	}

	// ── UDA002 ──────────────────────────────────────────────────────────────

	[Fact]
	public async Task CollectionReturn_NamedGetAllBy_ReportsDiagnosticUDA002()
	{
		const string source = RepositoryStubs + @"using System.Collections.Generic;
using System.Threading.Tasks;
using Umbrella.DataAccess.EntityFrameworkCore;

namespace TestApp
{
    public class ThingRepository : GenericDbRepository<object>
    {
        public Task<IReadOnlyCollection<object>> GetAllByStatusAsync() => Task.FromResult<IReadOnlyCollection<object>>(new List<object>());
    }
}";
		// Line 12: using System.Collections.Generic;
		// Line 13: using System.Threading.Tasks;
		// Line 14: using Umbrella.DataAccess.EntityFrameworkCore;
		// Line 15: (blank)
		// Line 16: namespace TestApp
		// Line 17: {
		// Line 18:     public class ThingRepository ...
		// Line 19:     {
		// Line 20:         public Task<IReadOnlyCollection<object>> GetAllByStatusAsync()
		//          "        public Task<IReadOnlyCollection<object>> " = 8+7+34 = 49 → col 50
		await VerifyAnalyzerAsync(source, Diagnostic(RepositoryMethodNamingAnalyzer.FindAllByRule, 20, 50, "GetAllByStatusAsync"));
	}

	[Fact]
	public async Task CollectionReturn_CorrectlyNamedFindAllBy_NoDiagnostic()
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
	public async Task PaginatedResultModelReturn_NamedGetAll_ReportsDiagnosticUDA002()
	{
		const string source = RepositoryStubs + @"using System.Threading.Tasks;
using Umbrella.DataAccess.EntityFrameworkCore;
using Umbrella.DataAccess.Abstractions;

namespace TestApp
{
    public class ThingRepository : GenericDbRepository<object>
    {
        public Task<PaginatedResultModel<object>> GetAllAsync() => Task.FromResult(new PaginatedResultModel<object>());
    }
}";
		// Line 20:         public Task<PaginatedResultModel<object>> GetAllAsync()
		//          "        public Task<PaginatedResultModel<object>> " = 8+7+35 = 50 → col 51
		await VerifyAnalyzerAsync(source, Diagnostic(RepositoryMethodNamingAnalyzer.FindAllByRule, 20, 51, "GetAllAsync"));
	}

	[Fact]
	public async Task IReadOnlyCollectionOfIOperationResult_NamedGetAllBy_ReportsDiagnosticUDA002()
	{
		const string source = RepositoryStubs + @"using System.Collections.Generic;
using System.Threading.Tasks;
using Umbrella.DataAccess.EntityFrameworkCore;
using Umbrella.DataAccess.Abstractions;

namespace TestApp
{
    public class ThingRepository : GenericDbRepository<object>
    {
        public Task<IReadOnlyCollection<IOperationResult<object>>> GetAllByStatusAsync() => Task.FromResult<IReadOnlyCollection<IOperationResult<object>>>(new List<IOperationResult<object>>());
    }
}";
		// Line 12: using System.Collections.Generic;
		// Line 13: using System.Threading.Tasks;
		// Line 14: using Umbrella.DataAccess.EntityFrameworkCore;
		// Line 15: using Umbrella.DataAccess.Abstractions;
		// Line 16: (blank)
		// Line 17: namespace TestApp
		// Line 18: {
		// Line 19:     public class ThingRepository ...
		// Line 20:     {
		// Line 21:         public Task<IReadOnlyCollection<IOperationResult<object>>> GetAllByStatusAsync()
		//          "        public Task<IReadOnlyCollection<IOperationResult<object>>> " = 8+7+52 = 67 → col 68
		await VerifyAnalyzerAsync(source, Diagnostic(RepositoryMethodNamingAnalyzer.FindAllByRule, 21, 68, "GetAllByStatusAsync"));
	}

	// ── UDA003 ──────────────────────────────────────────────────────────────

	[Fact]
	public async Task CountReturn_NamedCount_ReportsDiagnosticUDA003()
	{
		const string source = RepositoryStubs + @"using System.Threading.Tasks;
using Umbrella.DataAccess.EntityFrameworkCore;

namespace TestApp
{
    public class ThingRepository : GenericDbRepository<object>
    {
        public Task<int> CountByStatusAsync(string status) => Task.FromResult(0);
    }
}";
		// Line 19:         public Task<int> CountByStatusAsync(...)   ← col 26
		//          "        public Task<int> " = 8+7+10 = 25 → col 26
		await VerifyAnalyzerAsync(source, Diagnostic(RepositoryMethodNamingAnalyzer.FindCountRule, 19, 26, "CountByStatusAsync"));
	}

	[Fact]
	public async Task CountReturn_CorrectlyNamedFindCount_NoDiagnostic()
	{
		const string source = RepositoryStubs + @"using System.Threading.Tasks;
using Umbrella.DataAccess.EntityFrameworkCore;

namespace TestApp
{
    public class ThingRepository : GenericDbRepository<object>
    {
        public Task<int> FindCountByStatusAsync(string status) => Task.FromResult(0);
    }
}";
		await VerifyNoDiagnosticsAsync(source);
	}

	// ── UDA004 ──────────────────────────────────────────────────────────────

	[Fact]
	public async Task BoolReturn_NamedIsActive_ReportsDiagnosticUDA004()
	{
		const string source = RepositoryStubs + @"using System.Threading.Tasks;
using Umbrella.DataAccess.EntityFrameworkCore;

namespace TestApp
{
    public class ThingRepository : GenericDbRepository<object>
    {
        public Task<bool> IsActiveAsync(int id) => Task.FromResult(true);
    }
}";
		// Line 19:         public Task<bool> IsActiveAsync(...)   ← col 27
		//          "        public Task<bool> " = 8+7+11 = 26 → col 27
		await VerifyAnalyzerAsync(source, Diagnostic(RepositoryMethodNamingAnalyzer.ExistsRule, 19, 27, "IsActiveAsync"));
	}

	[Fact]
	public async Task BoolReturn_CorrectlyNamedExists_NoDiagnostic()
	{
		const string source = RepositoryStubs + @"using System.Threading.Tasks;
using Umbrella.DataAccess.EntityFrameworkCore;

namespace TestApp
{
    public class ThingRepository : GenericDbRepository<object>
    {
        public Task<bool> ExistsByEmailAsync(string email) => Task.FromResult(true);
    }
}";
		await VerifyNoDiagnosticsAsync(source);
	}

	// ── Exclusions ──────────────────────────────────────────────────────────

	[Fact]
	public async Task OverrideMethod_WrongNaming_NoDiagnostic()
	{
		const string source = RepositoryStubs + @"using System.Threading.Tasks;
using Umbrella.DataAccess.EntityFrameworkCore;

namespace TestApp
{
    public class ThingRepository : GenericDbRepository<object>
    {
        public override Task<object?> GetByIdAsync(int id) => Task.FromResult<object?>(null);
    }
}";
		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task StaticMethod_WrongNaming_NoDiagnostic()
	{
		const string source = RepositoryStubs + @"using System.Threading.Tasks;
using Umbrella.DataAccess.EntityFrameworkCore;

namespace TestApp
{
    public class ThingRepository : GenericDbRepository<object>
    {
        public static Task<object?> GetByIdAsync(int id) => Task.FromResult<object?>(null);
    }
}";
		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task NonPublicMethod_WrongNaming_NoDiagnostic()
	{
		const string source = RepositoryStubs + @"using System.Threading.Tasks;
using Umbrella.DataAccess.EntityFrameworkCore;

namespace TestApp
{
    public class ThingRepository : GenericDbRepository<object>
    {
        protected Task<object?> GetByIdAsync(int id) => Task.FromResult<object?>(null);
    }
}";
		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task NonRepositoryClass_WrongNaming_NoDiagnostic()
	{
		const string source = RepositoryStubs + @"using System.Threading.Tasks;

namespace TestApp
{
    public class ThingService
    {
        public Task<object?> GetByIdAsync(int id) => Task.FromResult<object?>(null);
    }
}";
		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task BareTaskReturn_NoDiagnostic()
	{
		const string source = RepositoryStubs + @"using System.Threading.Tasks;
using Umbrella.DataAccess.EntityFrameworkCore;

namespace TestApp
{
    public class ThingRepository : GenericDbRepository<object>
    {
        public Task DeleteByIdAsync(int id) => Task.CompletedTask;
    }
}";
		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task VoidReturn_NoDiagnostic()
	{
		const string source = RepositoryStubs + @"using Umbrella.DataAccess.EntityFrameworkCore;

namespace TestApp
{
    public class ThingRepository : GenericDbRepository<object>
    {
        public void Reset() { }
    }
}";
		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task IQueryableReturn_NoDiagnosticFromNamingAnalyzer()
	{
		// IQueryable returns are handled exclusively by UDA005 (RepositoryIQueryableAnalyzer);
		// the naming analyzer must not also fire on them.
		const string source = RepositoryStubs + @"using Umbrella.DataAccess.EntityFrameworkCore;

namespace System.Linq { public interface IQueryable<T> { } }

namespace TestApp
{
    public class ThingRepository : GenericDbRepository<object>
    {
        public System.Linq.IQueryable<object> GetItems() => null!;
    }
}";
		await VerifyNoDiagnosticsAsync(source);
	}
}
