using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Umbrella.AspNetCore.WebUtilities.Mvc;
using Umbrella.Utilities.Primitives;
using Umbrella.Utilities.Primitives.Abstractions;

namespace Umbrella.AspNetCore.WebUtilities.Test.Mvc;

public class UmbrellaDataServiceApiControllerTest
{
	public sealed record TestModel(int Id, string Name);

	public interface ITestDataService
	{
		Task<IOperationResult<TestModel?>> FindAsync(CancellationToken cancellationToken);

		Task<IOperationResult> RemoveAsync(CancellationToken cancellationToken);
	}

	[Fact]
	public async Task ExecuteOperationAsync_GenericSuccess_Returns200WithBody()
	{
		var model = new TestModel(1, "One");
		var controller = CreateController(service => service.Setup(x => x.FindAsync(It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult<TestModel?>.Success(model)));

		IActionResult result = await controller.GetAsync((service, token) => service.FindAsync(token), "Error.", cancellationToken: TestContext.Current.CancellationToken);

		var okResult = Assert.IsType<OkObjectResult>(result);
		Assert.Same(model, okResult.Value);
	}

	[Fact]
	public async Task ExecuteOperationAsync_CreatedWithResult_Returns201WithBody()
	{
		var model = new TestModel(1, "One");
		var controller = CreateController(service => service.Setup(x => x.FindAsync(It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult<TestModel?>.Created(model)));

		IActionResult result = await controller.GetAsync((service, token) => service.FindAsync(token), "Error.", cancellationToken: TestContext.Current.CancellationToken);

		var createdResult = Assert.IsType<CreatedResult>(result);
		Assert.Same(model, createdResult.Value);
	}

	[Fact]
	public async Task ExecuteOperationAsync_CreatedWithNullResult_Returns201WithoutBody()
	{
		var controller = CreateController(service => service.Setup(x => x.FindAsync(It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult<TestModel?>.Created(null)));

		IActionResult result = await controller.GetAsync((service, token) => service.FindAsync(token), "Error.", cancellationToken: TestContext.Current.CancellationToken);

		var statusCodeResult = Assert.IsType<StatusCodeResult>(result);
		Assert.Equal(StatusCodes.Status201Created, statusCodeResult.StatusCode);
	}

	[Fact]
	public async Task ExecuteOperationAsync_NonGenericSuccess_Returns200()
	{
		var controller = CreateController(service => service.Setup(x => x.RemoveAsync(It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult.Success()));

		IActionResult result = await controller.DeleteAsync((service, token) => service.RemoveAsync(token), "Error.", cancellationToken: TestContext.Current.CancellationToken);

		_ = Assert.IsType<OkResult>(result);
	}

	[Fact]
	public async Task ExecuteOperationAsync_NonGenericNoContent_Returns204()
	{
		var controller = CreateController(service => service.Setup(x => x.RemoveAsync(It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult.NoContent()));

		IActionResult result = await controller.DeleteAsync((service, token) => service.RemoveAsync(token), "Error.", cancellationToken: TestContext.Current.CancellationToken);

		_ = Assert.IsType<NoContentResult>(result);
	}

	[Fact]
	public async Task ExecuteOperationAsync_NotFound_Returns404Problem()
	{
		var controller = CreateController(service => service.Setup(x => x.FindAsync(It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult<TestModel?>.NotFound("Missing.")));

		IActionResult result = await controller.GetAsync((service, token) => service.FindAsync(token), "Error.", cancellationToken: TestContext.Current.CancellationToken);

		var objectResult = Assert.IsType<ObjectResult>(result);
		Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);

		var problemDetails = Assert.IsType<UmbrellaProblemDetails>(objectResult.Value);
		Assert.Equal("Missing.", problemDetails.Detail);
	}

	[Fact]
	public async Task ExecuteOperationAsync_OperationThrows_ProductionReturns500AndLogsCallerInfo()
	{
		var logger = new CapturingLogger();
		var controller = CreateController(
			service => service.Setup(x => x.FindAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("Boom.")),
			logger: logger);

		IActionResult result = await controller.GetAsync((service, token) => service.FindAsync(token), "The operation failed.", new { id = 42 }, TestContext.Current.CancellationToken);

		var objectResult = Assert.IsType<ObjectResult>(result);
		Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);

		var problemDetails = Assert.IsType<UmbrellaProblemDetails>(objectResult.Value);
		Assert.Equal("The operation failed.", problemDetails.Detail);

		string logMessage = Assert.Single(logger.Messages);
		Assert.Contains("methodName: GetAsync", logMessage, StringComparison.Ordinal);
		Assert.Contains("id: 42", logMessage, StringComparison.Ordinal);
	}

	[Fact]
	public async Task ExecuteOperationAsync_OperationThrows_NoLogState_StillReturns500()
	{
		var logger = new CapturingLogger();
		var controller = CreateController(
			service => service.Setup(x => x.FindAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("Boom.")),
			logger: logger);

		IActionResult result = await controller.GetAsync((service, token) => service.FindAsync(token), "The operation failed.", cancellationToken: TestContext.Current.CancellationToken);

		var objectResult = Assert.IsType<ObjectResult>(result);
		Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
		_ = Assert.Single(logger.Messages);
	}

	[Fact]
	public async Task ExecuteOperationAsync_OperationThrows_DevelopmentRethrows()
	{
		var controller = CreateController(
			service => service.Setup(x => x.FindAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("Boom.")),
			environmentName: "Development");

		_ = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.GetAsync((service, token) => service.FindAsync(token), "Error.", cancellationToken: TestContext.Current.CancellationToken));
	}

	[Fact]
	public async Task ExecuteOperationAsync_OperationCanceled_ProductionRethrows()
	{
		// NB: An enabled logger is required here. When logging is disabled, LogDetails short-circuits before
		// its cancellation check and the exception filter catches the OperationCanceledException instead of rethrowing it.
		var controller = CreateController(
			service => service.Setup(x => x.FindAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new OperationCanceledException()),
			logger: new CapturingLogger());

		_ = await Assert.ThrowsAsync<OperationCanceledException>(() => controller.GetAsync((service, token) => service.FindAsync(token), "Error.", cancellationToken: TestContext.Current.CancellationToken));
	}

	[Fact]
	public async Task ExecuteOperationAsync_PreCancelledToken_ThrowsWithoutTouchingService()
	{
		bool serviceCreated = false;
		var lazyService = new Lazy<ITestDataService>(() =>
		{
			serviceCreated = true;
			return Mock.Of<ITestDataService>();
		});

		var controller = new TestDataServiceApiController(NullLogger.Instance, CreateWebHostEnvironment("Production"), lazyService);

		using var cancellationTokenSource = new CancellationTokenSource();
		await cancellationTokenSource.CancelAsync();

		_ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => controller.GetAsync((service, token) => service.FindAsync(token), "Error.", cancellationToken: cancellationTokenSource.Token));
		Assert.False(serviceCreated);
	}

	private static TestDataServiceApiController CreateController(Action<Mock<ITestDataService>> setup, ILogger? logger = null, string environmentName = "Production")
	{
		var serviceMock = new Mock<ITestDataService>();
		setup(serviceMock);

		return new TestDataServiceApiController(logger ?? NullLogger.Instance, CreateWebHostEnvironment(environmentName), new Lazy<ITestDataService>(() => serviceMock.Object));
	}

	private static IWebHostEnvironment CreateWebHostEnvironment(string environmentName)
	{
		var webHostEnvironment = new Mock<IWebHostEnvironment>();
		_ = webHostEnvironment.Setup(x => x.EnvironmentName).Returns(environmentName);

		return webHostEnvironment.Object;
	}

	private sealed class TestDataServiceApiController : UmbrellaDataServiceApiController<ITestDataService>
	{
		public TestDataServiceApiController(ILogger logger, IWebHostEnvironment hostingEnvironment, Lazy<ITestDataService> dataService)
			: base(logger, hostingEnvironment, dataService)
		{
			ControllerContext = new ControllerContext
			{
				HttpContext = new DefaultHttpContext()
			};
		}

		public Task<IActionResult> GetAsync(Func<ITestDataService, CancellationToken, Task<IOperationResult<TestModel?>>> operation, string errorMessage, object? logState = null, CancellationToken cancellationToken = default)
			=> ExecuteOperationAsync(operation, errorMessage, cancellationToken, logState);

		public Task<IActionResult> DeleteAsync(Func<ITestDataService, CancellationToken, Task<IOperationResult>> operation, string errorMessage, object? logState = null, CancellationToken cancellationToken = default)
			=> ExecuteOperationAsync(operation, errorMessage, cancellationToken, logState);
	}

	private sealed class CapturingLogger : ILogger
	{
		public List<string> Messages { get; } = [];

		public IDisposable? BeginScope<TState>(TState state)
			where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
	}
}
