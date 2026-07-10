using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Umbrella.AspNetCore.WebUtilities.Mvc;
using Umbrella.Utilities.Data.Abstractions;
using Umbrella.Utilities.Data.Pagination;
using Umbrella.Utilities.Data.Services.Abstractions;
using Umbrella.Utilities.Primitives;

namespace Umbrella.AspNetCore.WebUtilities.Test.Mvc;

public class UmbrellaGenericRepositoryDataServiceApiControllerTest
{
	public sealed class TestItem : IKeyedItem<int>
	{
		public int Id { get; init; }
	}

	public sealed class TestSlimItem : IKeyedItem<int>
	{
		public int Id { get; init; }
	}

	public sealed class TestPaginatedResultModel : PaginatedResultModel<TestSlimItem>
	{
	}

	public sealed class TestCreateItem
	{
	}

	public sealed class TestCreateResult
	{
	}

	public sealed class TestUpdateItem : IKeyedItem<int>
	{
		public int Id { get; init; }
	}

	public sealed class TestUpdateResult
	{
	}

	public interface ITestGenericDataService : IGenericDataService<TestItem, int, TestSlimItem, TestPaginatedResultModel, TestCreateItem, TestCreateResult, TestUpdateItem, TestUpdateResult>
	{
	}

	[Fact]
	public async Task GetAsync_Success_Returns200WithBody()
	{
		var item = new TestItem { Id = 1 };
		var controller = CreateController(service => service.Setup(x => x.FindByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult<TestItem?>.Success(item)));

		IActionResult result = await controller.GetAsync(1, TestContext.Current.CancellationToken);

		var okResult = Assert.IsType<OkObjectResult>(result);
		Assert.Same(item, okResult.Value);
	}

	[Fact]
	public async Task GetAsync_NotFound_Returns404Problem()
	{
		var controller = CreateController(service => service.Setup(x => x.FindByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult<TestItem?>.NotFound("Missing.")));

		IActionResult result = await controller.GetAsync(1, TestContext.Current.CancellationToken);

		var objectResult = Assert.IsType<ObjectResult>(result);
		Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
		_ = Assert.IsType<UmbrellaProblemDetails>(objectResult.Value);
	}

	[Fact]
	public async Task DeleteAsync_NoContent_Returns204()
	{
		var controller = CreateController(service => service.Setup(x => x.DeleteAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult.NoContent()));

		IActionResult result = await controller.DeleteAsync(1, TestContext.Current.CancellationToken);

		_ = Assert.IsType<NoContentResult>(result);
	}

	[Fact]
	public async Task PostAsync_ServiceThrows_ProductionReturns500WithExactMessage()
	{
		var controller = CreateController(service => service.Setup(x => x.CreateAsync(It.IsAny<TestCreateItem>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("Boom.")));

		IActionResult result = await controller.PostAsync(new TestCreateItem(), TestContext.Current.CancellationToken);

		var objectResult = Assert.IsType<ObjectResult>(result);
		Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);

		var problemDetails = Assert.IsType<UmbrellaProblemDetails>(objectResult.Value);
		Assert.Equal("An error occurred while attempting to create the requested resource.", problemDetails.Detail);
	}

	[Fact]
	public async Task ExistsByIdAsync_Success_Returns200WithBooleanBody()
	{
		var controller = CreateController(service => service.Setup(x => x.ExistsByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult<bool>.Success(true)));

		IActionResult result = await controller.ExistsByIdAsync(1, TestContext.Current.CancellationToken);

		var okResult = Assert.IsType<OkObjectResult>(result);
		Assert.Equal(true, okResult.Value);
	}

	private static TestGenericRepositoryDataServiceApiController CreateController(Action<Mock<ITestGenericDataService>> setup)
	{
		var serviceMock = new Mock<ITestGenericDataService>();
		setup(serviceMock);

		return new TestGenericRepositoryDataServiceApiController(NullLogger.Instance, CreateWebHostEnvironment(), new Lazy<ITestGenericDataService>(() => serviceMock.Object));
	}

	private static IWebHostEnvironment CreateWebHostEnvironment()
	{
		var webHostEnvironment = new Mock<IWebHostEnvironment>();
		_ = webHostEnvironment.Setup(x => x.EnvironmentName).Returns("Production");

		return webHostEnvironment.Object;
	}

	private sealed class TestGenericRepositoryDataServiceApiController : UmbrellaGenericRepositoryDataServiceApiController<TestSlimItem, TestPaginatedResultModel, TestItem, TestCreateItem, TestCreateResult, TestUpdateItem, TestUpdateResult, int, ITestGenericDataService>
	{
		public TestGenericRepositoryDataServiceApiController(ILogger logger, IWebHostEnvironment hostingEnvironment, Lazy<ITestGenericDataService> repositoryDataService)
			: base(logger, hostingEnvironment, repositoryDataService)
		{
			ControllerContext = new ControllerContext
			{
				HttpContext = new DefaultHttpContext()
			};
		}
	}
}
