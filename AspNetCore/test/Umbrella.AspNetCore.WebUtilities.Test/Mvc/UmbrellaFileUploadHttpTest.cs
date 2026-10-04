using System.Net.Http;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Testcontainers.Azurite;
using Umbrella.AspNetCore.WebUtilities.FileSystem.Mvc;
using Umbrella.FileSystem.Abstractions;
using Umbrella.FileSystem.AzureStorage;
using Umbrella.FileSystem.Disk;
using Umbrella.Internal.Mocks;

namespace Umbrella.AspNetCore.WebUtilities.Test.Mvc;

public class UmbrellaFileUploadHttpTest
{
	[Fact]
	public async Task ConcreteControllers_ControlAuthorization_AndStreamToAzurite()
	{
		var cancellationToken = TestContext.Current.CancellationToken;
		await using var container = new AzuriteBuilder("mcr.microsoft.com/azure-storage/azurite:3.37.0")
			.WithInMemoryPersistence()
			// Azure Storage SDK releases can request API versions newer than Azurite supports.
			.WithCommand("--skipApiVersionCheck")
			.Build();
		await container.StartAsync(cancellationToken);
		using var provider = new UmbrellaAzureBlobStorageFileProvider(
			CoreUtilitiesMocks.CreateLoggerFactory<UmbrellaAzureBlobStorageFileProvider>(),
			CoreUtilitiesMocks.CreateMimeTypeUtility(("png", "image/png")),
			CoreUtilitiesMocks.CreateGenericTypeConverter(),
			new UmbrellaFileAuthorizationHandlerRegistry([]));
		provider.InitializeOptions(new UmbrellaAzureBlobStorageFileProviderOptions
		{
			StorageConnectionString = container.GetConnectionString(),
			AllowUnhandledFileAuthorizationChecks = true
		});
		await VerifyUploadAsync(provider, cancellationToken);
	}

	[Fact]
	public async Task ConcreteControllers_ControlAuthorization_AndStreamToDisk()
	{
		var directory = Directory.CreateTempSubdirectory("umbrella-upload-test-");
		try
		{
			var provider = new UmbrellaDiskFileStorageProvider(
				CoreUtilitiesMocks.CreateLoggerFactory<UmbrellaDiskFileStorageProvider>(),
				CoreUtilitiesMocks.CreateMimeTypeUtility(("png", "image/png")),
				CoreUtilitiesMocks.CreateGenericTypeConverter(),
				new UmbrellaFileAuthorizationHandlerRegistry([]));
			provider.InitializeOptions(new UmbrellaDiskFileStorageProviderOptions
			{
				RootPhysicalPath = directory.FullName,
				AllowUnhandledFileAuthorizationChecks = true
			});
			// Existing callers can still supply seekable streams positioned after the beginning.
			using var stream = new MemoryStream([1, 2, 3, 4]);
			stream.Position = 2;
			var file = await provider.SaveAsync("/temp/seekable.png", stream, cancellationToken: TestContext.Current.CancellationToken);
			byte[] actualBytes = await file.ReadAsByteArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
			Assert.Equal<byte>([1, 2, 3, 4], actualBytes);
			Assert.True(stream.CanRead);

			await VerifyUploadAsync(provider, TestContext.Current.CancellationToken);
		}
		finally
		{
			directory.Delete(recursive: true);
		}
	}

	private static async Task VerifyUploadAsync(IUmbrellaFileStorageProvider provider, CancellationToken cancellationToken)
	{
		var handler = new Mock<IUmbrellaTempFileHandler>();
		_ = handler.Setup(x => x.GetTempFilePath(It.IsAny<string>())).Returns((string name) => "/temp/" + name);
		_ = handler.Setup(x => x.GetTempWebFilePath(It.IsAny<string>())).Returns((string name) => "/files/temp/" + name);

		var builder = WebApplication.CreateBuilder();
		_ = builder.WebHost.UseUrls("http://127.0.0.1:0");
		_ = builder.Services.AddSingleton<IUmbrellaFileStorageProvider>(provider);
		_ = builder.Services.AddSingleton(handler.Object);
		_ = builder.Services.AddControllers().AddApplicationPart(typeof(OpenUploadController).Assembly);
		_ = builder.Services.AddAuthentication("upload-test").AddScheme<AuthenticationSchemeOptions, UploadAuthenticationHandler>("upload-test", _ => { });
		_ = builder.Services.AddAuthorization();
		await using var app = builder.Build();
		_ = app.UseAuthentication();
		_ = app.UseAuthorization();
		_ = app.MapControllers();
		await app.StartAsync(cancellationToken);
		using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };

		try
		{
			using var deniedRequest = CreateRequest("secured-upload", authenticated: false);
			using var denied = await client.SendAsync(deniedRequest, cancellationToken);
			Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);

			foreach (var (path, authenticated) in new[] { ("open-upload", false), ("secured-upload", true) })
			{
				using var request = CreateRequest(path, authenticated);
				using var response = await client.SendAsync(request, cancellationToken);
				Assert.Equal(HttpStatusCode.Created, response.StatusCode);
				string name = await response.Content.ReadAsStringAsync(cancellationToken);
				Assert.Equal("/files/temp/" + name, response.Headers.Location!.OriginalString);
				var file = await provider.GetAsync("/temp/" + name, cancellationToken);
				Assert.NotNull(file);
				byte[] actualBytes = await file.ReadAsByteArrayAsync(cancellationToken: cancellationToken);
				Assert.Equal<byte>([1, 2, 3, 4], actualBytes);
				Assert.Equal("image.png", await file.GetFileNameAsync(cancellationToken));
				Assert.Equal(UmbrellaFileUploadControllerTest.Purpose.Image, await file.GetFileUploadTypeAsync<UmbrellaFileUploadControllerTest.Purpose>(cancellationToken));
				_ = await file.DeleteAsync(cancellationToken);
			}
		}
		finally
		{
			await app.StopAsync(CancellationToken.None);
		}
	}

	private static HttpRequestMessage CreateRequest(string path, bool authenticated)
	{
		var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = new LengthlessContent() };
		request.Headers.Add("X-FileName", "image.png");
		request.Headers.Add("X-FileUploadType", "Image");
		if (authenticated)
			request.Headers.Add("X-Test-User", "test-user");
		return request;
	}

	private sealed class LengthlessContent : HttpContent
	{
		protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(new byte[] { 1, 2, 3, 4 }).AsTask();
		protected override bool TryComputeLength(out long length)
		{
			length = 0;
			return false;
		}
	}
}

[Route("open-upload")]
public class OpenUploadController : UmbrellaFileUploadController<UmbrellaFileUploadControllerTest.Purpose>
{
	public OpenUploadController(ILogger<OpenUploadController> logger, IWebHostEnvironment environment, IUmbrellaFileStorageProvider provider, IUmbrellaTempFileHandler handler)
		: base(logger, environment, provider, handler) { }

	protected override Task<IActionResult?> ValidateUploadAsync(UmbrellaFileUploadContext<UmbrellaFileUploadControllerTest.Purpose> context, CancellationToken cancellationToken)
	{
		Assert.False(Request.Body.CanSeek);
		Assert.Null(Request.ContentLength);
		return base.ValidateUploadAsync(context, cancellationToken);
	}
}

[Authorize]
[Route("secured-upload")]
public class SecuredUploadController : UmbrellaFileUploadController<UmbrellaFileUploadControllerTest.Purpose>
{
	public SecuredUploadController(ILogger<SecuredUploadController> logger, IWebHostEnvironment environment, IUmbrellaFileStorageProvider provider, IUmbrellaTempFileHandler handler)
		: base(logger, environment, provider, handler) { }
}

public class UploadAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
	public UploadAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
		: base(options, logger, encoder) { }
	protected override Task<AuthenticateResult> HandleAuthenticateAsync()
		=> Task.FromResult(Request.Headers.ContainsKey("X-Test-User")
			? AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "test-user")], Scheme.Name)), Scheme.Name))
			: AuthenticateResult.NoResult());
}
