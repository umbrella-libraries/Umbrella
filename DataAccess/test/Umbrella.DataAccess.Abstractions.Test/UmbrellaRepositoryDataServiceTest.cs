using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Umbrella.DataAccess.Abstractions.Options;
using Umbrella.Utilities.Data.Abstractions;
using Umbrella.Utilities.Data.Filtering;
using Umbrella.Utilities.Data.Models;
using Umbrella.Utilities.Data.Pagination;
using Umbrella.Utilities.Data.Sorting;
using Umbrella.Utilities.Mapping.Abstractions;
using Umbrella.Utilities.Primitives;
using Umbrella.Utilities.Primitives.Abstractions;
using Umbrella.Utilities.Security.Abstractions;
using Umbrella.Utilities.Threading.Abstractions;

namespace Umbrella.DataAccess.Abstractions.Test;

public class UmbrellaRepositoryDataServiceTest
{
	[Fact]
	public async Task FindAllSlimAsync_BeforeSearchReturnsFailure_ShortCircuitsBeforeRepositoryAccess()
	{
		var repository = new Mock<ITestRepository>();
		TestRepositoryDataService service = CreateService(repository, beforeSearchSlimResult: new OperationResult(OperationResultStatus.InvalidOperation));

		IOperationResult<TestPaginatedResultModel?> result = await service.FindAllSlimAsync(cancellationToken: TestContext.Current.CancellationToken);

		Assert.Equal(OperationResultStatus.InvalidOperation, result.Status);
		Assert.True(service.BeforeSearchSlimCalled);
		repository.VerifyNoOtherCalls();
	}

	[Fact]
	public async Task FindByIdAsync_BeforeReadReturnsFailure_ShortCircuitsBeforeRepositoryAccess()
	{
		var repository = new Mock<ITestRepository>();
		TestRepositoryDataService service = CreateService(repository, beforeReadResult: new OperationResult(OperationResultStatus.InvalidOperation));

		IOperationResult<TestItem?> result = await service.FindByIdAsync(1, TestContext.Current.CancellationToken);

		Assert.Equal(OperationResultStatus.InvalidOperation, result.Status);
		Assert.True(service.BeforeReadCalled);
		repository.VerifyNoOtherCalls();
	}

	private static TestRepositoryDataService CreateService(
		Mock<ITestRepository> repository,
		IOperationResult? beforeSearchSlimResult = null,
		IOperationResult? beforeReadResult = null)
	{
		var hostingEnvironment = new Mock<IHostEnvironment>();
		_ = hostingEnvironment.Setup(x => x.EnvironmentName).Returns("Production");

		return new TestRepositoryDataService(
			NullLogger<UmbrellaRepositoryCoreDataService>.Instance,
			hostingEnvironment.Object,
			new UmbrellaRepositoryDataServiceOptions(),
			new Mock<IUmbrellaMapper>().Object,
			new Mock<IUmbrellaAuthorizationService>().Object,
			new Mock<ISynchronizationManager>().Object,
			new Lazy<IDataAccessUnitOfWork>(() => new Mock<IDataAccessUnitOfWork>().Object),
			new Lazy<ITestRepository>(() => repository.Object),
			new Mock<IDataExpressionFactory>().Object,
			beforeSearchSlimResult,
			beforeReadResult);
	}

	private sealed class TestEntity : IEntity<int>
	{
		public int Id { get; set; }

		public void TrimAllStringProperties()
		{
		}
	}

	private sealed class TestSlimItem : IKeyedItem<int>
	{
		public int Id { get; init; }
	}

	private sealed record TestPaginatedResultModel : PaginatedResultModel<TestSlimItem>;

	private sealed class TestItem : IKeyedItem<int>
	{
		public int Id { get; init; }
	}

	private sealed class TestCreateItem;

	private sealed class TestCreateResult : ICreateResultModel<int>
	{
		public int Id { get; init; }
	}

	private sealed class TestUpdateItem : IUpdateModel<int>
	{
		public int Id { get; init; }

		public string ConcurrencyStamp { get; set; } = string.Empty;
	}

	private sealed class TestUpdateResult : IUpdateResultModel
	{
		public string ConcurrencyStamp { get; init; } = string.Empty;
	}

	private interface ITestRepository : IGenericDbRepository<TestEntity, RepoOptions, int>;

	private sealed class TestRepositoryDataService : UmbrellaRepositoryDataService<TestItem, TestSlimItem, TestPaginatedResultModel, TestCreateItem, TestCreateResult, TestUpdateItem, TestUpdateResult, ITestRepository, TestEntity, RepoOptions, int>
	{
		private readonly IOperationResult? _beforeSearchSlimResult;
		private readonly IOperationResult? _beforeReadResult;

		public bool BeforeSearchSlimCalled { get; private set; }
		public bool BeforeReadCalled { get; private set; }

		public TestRepositoryDataService(
			Microsoft.Extensions.Logging.ILogger<UmbrellaRepositoryCoreDataService> logger,
			IHostEnvironment hostingEnvironment,
			UmbrellaRepositoryDataServiceOptions options,
			IUmbrellaMapper mapper,
			IUmbrellaAuthorizationService authorizationService,
			ISynchronizationManager synchronizationManager,
			Lazy<IDataAccessUnitOfWork> dataAccessUnitOfWork,
			Lazy<ITestRepository> repository,
			IDataExpressionFactory dataExpressionFactory,
			IOperationResult? beforeSearchSlimResult,
			IOperationResult? beforeReadResult)
			: base(logger, hostingEnvironment, options, mapper, authorizationService, synchronizationManager, dataAccessUnitOfWork, repository, dataExpressionFactory)
		{
			_beforeSearchSlimResult = beforeSearchSlimResult;
			_beforeReadResult = beforeReadResult;
		}

		protected override Task<IOperationResult?> BeforeSearchSlimAsync(
			int pageNumber,
			int pageSize,
			SortExpression<TestEntity>[]? sorters,
			FilterExpression<TestEntity>[]? filters,
			FilterExpressionCombinator? filterCombinator,
			CancellationToken cancellationToken)
		{
			BeforeSearchSlimCalled = true;
			return Task.FromResult(_beforeSearchSlimResult);
		}

		protected override Task<IOperationResult?> BeforeReadAsync(int id, CancellationToken cancellationToken)
		{
			BeforeReadCalled = true;
			return Task.FromResult(_beforeReadResult);
		}
	}
}
