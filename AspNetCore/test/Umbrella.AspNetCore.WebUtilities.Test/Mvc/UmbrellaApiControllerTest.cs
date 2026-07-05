using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Umbrella.AspNetCore.WebUtilities.Mvc;
using Umbrella.Utilities.Http.Constants;
using Umbrella.Utilities.Primitives;

namespace Umbrella.AspNetCore.WebUtilities.Test.Mvc;

public class UmbrellaApiControllerTest
{
	[Fact]
	public void OperationResult_WhenConcurrencyConflict_ReturnsConcurrencyConflictProblem()
	{
		var controller = new TestUmbrellaApiController();

		IActionResult result = controller.MapOperationResult(OperationResult.ConcurrencyConflict("Changed elsewhere."));

		AssertConcurrencyProblem(result, "Changed elsewhere.");
	}

	[Fact]
	public void OperationResult_WhenConflict_ReturnsGenericConflictProblem()
	{
		var controller = new TestUmbrellaApiController();

		IActionResult result = controller.MapOperationResult(OperationResult.Conflict("Already exists."));

		var objectResult = Assert.IsType<ObjectResult>(result);
		Assert.Equal(StatusCodes.Status409Conflict, objectResult.StatusCode);

		var problemDetails = Assert.IsType<UmbrellaProblemDetails>(objectResult.Value);
		Assert.Equal("Conflict", problemDetails.Title);
		Assert.Null(problemDetails.Code);
		Assert.Equal("Already exists.", problemDetails.Detail);
	}

	[Fact]
	public void OperationResultFailure_WhenConcurrencyConflict_ReturnsConcurrencyConflictProblem()
	{
		var controller = new TestUmbrellaApiController();
		var exception = new OperationResultException(
			OperationResultStatus.ConcurrencyConflict,
			[new ValidationResult("Changed elsewhere.")]);

		IActionResult result = controller.MapOperationResultFailure(exception);

		AssertConcurrencyProblem(result, "Changed elsewhere.");
	}

	private static void AssertConcurrencyProblem(IActionResult result, string expectedDetail)
	{
		var objectResult = Assert.IsType<ObjectResult>(result);
		Assert.Equal(StatusCodes.Status409Conflict, objectResult.StatusCode);

		var problemDetails = Assert.IsType<UmbrellaProblemDetails>(objectResult.Value);
		Assert.Equal("Concurrency Conflict", problemDetails.Title);
		Assert.Equal(HttpProblemCodes.ConcurrencyStampMismatch, problemDetails.Code);
		Assert.Equal(expectedDetail, problemDetails.Detail);
	}

	private sealed class TestUmbrellaApiController : UmbrellaApiController
	{
		public TestUmbrellaApiController()
			: base(NullLogger<TestUmbrellaApiController>.Instance, CreateWebHostEnvironment())
		{
			ControllerContext = new ControllerContext
			{
				HttpContext = new DefaultHttpContext()
			};
		}

		public IActionResult MapOperationResult(OperationResult result) => OperationResult(result);

		public IActionResult MapOperationResultFailure(OperationResultException exception) => OperationResultFailure(exception);

		private static IWebHostEnvironment CreateWebHostEnvironment()
		{
			var webHostEnvironment = new Mock<IWebHostEnvironment>();
			_ = webHostEnvironment.Setup(x => x.EnvironmentName).Returns("Production");

			return webHostEnvironment.Object;
		}
	}
}
