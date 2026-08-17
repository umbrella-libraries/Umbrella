using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Umbrella.AspNetCore.WebUtilities.Mvc;
using Umbrella.DataAccess.Abstractions;
using Umbrella.DataAccess.Abstractions.Options;
using Umbrella.Utilities.Data.Filtering;
using Umbrella.Utilities.Data.Models;
using Umbrella.Utilities.Data.Pagination;
using Umbrella.Utilities.Data.Sorting;
using Umbrella.Utilities.Mapping.Abstractions;
using Umbrella.Utilities.Primitives;
using Umbrella.Utilities.Primitives.Abstractions;
using Umbrella.Utilities.Security.Abstractions;
using Umbrella.Utilities.Threading.Abstractions;

namespace Umbrella.AspNetCore.WebUtilities.Test.Mvc;

public class UmbrellaGenericRepositoryApiControllerTest
{
	[Fact]
	public async Task SearchSlimAsync_BeforeSearchReturnsFailure_ShortCircuitsWithMappedResult()
	{
		TestGenericRepositoryApiController controller = CreateController(beforeSearchSlimResult: new OperationResult(OperationResultStatus.InvalidOperation));

		IActionResult result = await controller.SearchSlimAsync(1, 10, cancellationToken: TestContext.Current.CancellationToken);

		var objectResult = Assert.IsType<ObjectResult>(result);
		var problemDetails = Assert.IsType<UmbrellaValidationProblemDetails>(objectResult.Value);
		Assert.Equal(StatusCodes.Status400BadRequest, problemDetails.Status);
		Assert.True(controller.BeforeSearchSlimCalled);
	}

	[Fact]
	public async Task GetAsync_BeforeReadReturnsFailure_ShortCircuitsWithMappedResult()
	{
		TestGenericRepositoryApiController controller = CreateController(beforeReadResult: new OperationResult(OperationResultStatus.InvalidOperation));

		IActionResult result = await controller.GetAsync(1, TestContext.Current.CancellationToken);

		var objectResult = Assert.IsType<ObjectResult>(result);
		var problemDetails = Assert.IsType<UmbrellaValidationProblemDetails>(objectResult.Value);
		Assert.Equal(StatusCodes.Status400BadRequest, problemDetails.Status);
		Assert.True(controller.BeforeReadCalled);
	}

	private static TestGenericRepositoryApiController CreateController(
		IOperationResult? beforeSearchSlimResult = null,
		IOperationResult? beforeReadResult = null)
	{
		var repository = new Mock<ITestRepository>();
		IWebHostEnvironment hostingEnvironment = CreateWebHostEnvironment();
		var mapper = new Mock<IUmbrellaMapper>();
		var dataAccessService = new UmbrellaRepositoryCoreDataService(
			NullLogger<UmbrellaRepositoryCoreDataService>.Instance,
			hostingEnvironment,
			new UmbrellaRepositoryDataServiceOptions(),
			mapper.Object,
			new Mock<IUmbrellaAuthorizationService>().Object,
			new Mock<ISynchronizationManager>().Object,
			new Lazy<IDataAccessUnitOfWork>(() => new Mock<IDataAccessUnitOfWork>().Object));

		return new TestGenericRepositoryApiController(
			NullLogger.Instance,
			hostingEnvironment,
			mapper.Object,
			new Lazy<ITestRepository>(() => repository.Object),
			dataAccessService,
			beforeSearchSlimResult,
			beforeReadResult);
	}

	private static IWebHostEnvironment CreateWebHostEnvironment()
	{
		var webHostEnvironment = new Mock<IWebHostEnvironment>();
		_ = webHostEnvironment.Setup(x => x.EnvironmentName).Returns("Production");

		return webHostEnvironment.Object;
	}

	private sealed class TestEntity : IEntity<int>
	{
		public int Id { get; set; }

		public void TrimAllStringProperties()
		{
		}
	}

	private sealed class TestSlimModel
	{
	}

	private sealed record TestPaginatedResultModel : PaginatedResultModel<TestSlimModel>;

	private sealed class TestModel
	{
	}

	private sealed class TestCreateModel
	{
	}

	private sealed class TestCreateResultModel : ICreateResultModel<int>
	{
		public int Id { get; init; }
	}

	private sealed class TestUpdateModel : IUpdateModel<int>
	{
		public int Id { get; init; }

		public string ConcurrencyStamp { get; set; } = string.Empty;
	}

	private sealed class TestUpdateResultModel : IUpdateResultModel
	{
		public string ConcurrencyStamp { get; init; } = string.Empty;
	}

	private interface ITestRepository : IGenericDbRepository<TestEntity, RepoOptions, int>;

	private sealed class TestGenericRepositoryApiController : UmbrellaGenericRepositoryApiController<TestSlimModel, TestPaginatedResultModel, TestModel, TestCreateModel, TestCreateResultModel, TestUpdateModel, TestUpdateResultModel, ITestRepository, TestEntity, RepoOptions, int>
	{
		private readonly IOperationResult? _beforeSearchSlimResult;
		private readonly IOperationResult? _beforeReadResult;

		public bool BeforeSearchSlimCalled { get; private set; }
		public bool BeforeReadCalled { get; private set; }

		public TestGenericRepositoryApiController(
			ILogger logger,
			IWebHostEnvironment hostingEnvironment,
			IUmbrellaMapper mapper,
			Lazy<ITestRepository> repository,
			IUmbrellaRepositoryCoreDataService dataAccessService,
			IOperationResult? beforeSearchSlimResult,
			IOperationResult? beforeReadResult)
			: base(logger, hostingEnvironment, mapper, repository, dataAccessService)
		{
			_beforeSearchSlimResult = beforeSearchSlimResult;
			_beforeReadResult = beforeReadResult;
			ControllerContext = new ControllerContext
			{
				HttpContext = new DefaultHttpContext()
			};
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
