using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Umbrella.AppFramework.Shared.Constants;
using Umbrella.AspNetCore.WebUtilities.Mvc;
using Umbrella.FileSystem.Abstractions;

namespace Umbrella.AspNetCore.WebUtilities.FileSystem.Mvc;

/// <summary>Streams a raw request body to temporary storage using an application-defined upload purpose.</summary>
/// <typeparam name="TUploadType">The application's upload-purpose enumeration.</typeparam>
/// <remarks>
/// Concrete controllers must supply routing, authorization and request-size limits.
/// Hooks run in validation, stream preparation, metadata, then response order.
/// The request body is never disposed by this controller. Preparation overrides must dispose
/// allocations on rejection or failure before transferring ownership of a replacement stream.
/// </remarks>
public abstract class UmbrellaFileUploadController<TUploadType> : UmbrellaApiController
	where TUploadType : struct, Enum
{
	/// <summary>Gets the file storage provider.</summary>
	protected IUmbrellaFileStorageProvider FileProvider { get; }
	/// <summary>Gets the temporary-file handler.</summary>
	protected IUmbrellaTempFileHandler TempFileHandler { get; }

	/// <summary>Gets the original-filename header name.</summary>
	protected virtual string FileNameHeaderName => FileUploadHeaderNames.Name;
	/// <summary>Gets the declared content-type header name.</summary>
	protected virtual string ContentTypeHeaderName => FileUploadHeaderNames.ContentType;
	/// <summary>Gets the upload-purpose header name.</summary>
	protected virtual string UploadTypeHeaderName => FileUploadHeaderNames.UploadType;
	/// <summary>Gets the simulated upload delay, applied only in Development. Defaults to zero.</summary>
	protected virtual TimeSpan DevelopmentUploadDelay => TimeSpan.Zero;

	/// <summary>Initializes the upload controller.</summary>
	/// <param name="logger">The logger.</param>
	/// <param name="hostingEnvironment">The hosting environment.</param>
	/// <param name="fileProvider">The file storage provider.</param>
	/// <param name="tempFileHandler">The temporary-file handler.</param>
	protected UmbrellaFileUploadController(
		ILogger logger,
		IWebHostEnvironment hostingEnvironment,
		IUmbrellaFileStorageProvider fileProvider,
		IUmbrellaTempFileHandler tempFileHandler)
		: base(logger, hostingEnvironment)
	{
		ArgumentNullException.ThrowIfNull(fileProvider);
		ArgumentNullException.ThrowIfNull(tempFileHandler);
		FileProvider = fileProvider;
		TempFileHandler = tempFileHandler;
	}

	/// <summary>Accepts defined enum values. Override to reject application sentinel or unsupported values.</summary>
	/// <param name="uploadType">The parsed purpose.</param>
	/// <returns>Whether the upload purpose is allowed.</returns>
	protected virtual bool IsUploadTypeAllowed(TUploadType uploadType) => Enum.IsDefined(uploadType);

	/// <summary>Validates request information before reading content or saving a file.</summary>
	/// <param name="context">The upload information.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A rejection response, or null to continue.</returns>
	protected virtual Task<IActionResult?> ValidateUploadAsync(UmbrellaFileUploadContext<TUploadType> context, CancellationToken cancellationToken)
		=> Task.FromResult<IActionResult?>(null);

	/// <summary>Prepares content for upload. By default forwards the unbuffered request body without ownership.</summary>
	/// <param name="context">The upload information.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A stream with explicit ownership, or a rejection response.</returns>
	/// <remarks>Rewind replacement buffers before returning them. Wrappers around Request.Body must leave it open.</remarks>
	protected virtual Task<UmbrellaFileUploadStreamResult> PrepareUploadStreamAsync(UmbrellaFileUploadContext<TUploadType> context, CancellationToken cancellationToken)
		=> Task.FromResult(UmbrellaFileUploadStreamResult.Success(Request.Body, ownsStream: false));

	/// <summary>Writes the original filename, purpose and temporary-file permissions, then persists metadata.</summary>
	/// <param name="context">The upload information.</param>
	/// <param name="fileInfo">The saved file.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <remarks>Overrides should call base to preserve standard metadata and permission application.</remarks>
	protected virtual async Task WriteUploadMetadataAsync(UmbrellaFileUploadContext<TUploadType> context, IUmbrellaFileInfo fileInfo, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(fileInfo);
		await fileInfo.SetFileNameAsync(context.FileName, false, cancellationToken).ConfigureAwait(false);
		await fileInfo.SetFileUploadTypeAsync(context.UploadType, false, cancellationToken).ConfigureAwait(false);
		await TempFileHandler.ApplyPermissionsAsync(fileInfo, default, false, cancellationToken).ConfigureAwait(false);
		await fileInfo.WriteMetadataChangesAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <summary>Creates the successful response with the temporary filename and URL.</summary>
	/// <param name="context">The upload information.</param>
	/// <param name="fileInfo">The saved file.</param>
	/// <returns>The upload response.</returns>
	protected virtual IActionResult CreateUploadResult(UmbrellaFileUploadContext<TUploadType> context, IUmbrellaFileInfo fileInfo)
	{
		ArgumentNullException.ThrowIfNull(context);
		return Created(context.TempUrl, context.TempFileName);
	}

	/// <summary>Validates and stores an uploaded request body.</summary>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The upload response or a validation problem.</returns>
	[HttpPost]
	public async Task<IActionResult> PostAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		UmbrellaFileUploadContext<TUploadType>? context = null;
		UmbrellaFileUploadStreamResult? prepared = null;
		bool storageAttempted = false;
		bool completed = false;

		try
		{
			if (IsDevelopment && DevelopmentUploadDelay > TimeSpan.Zero)
				await Task.Delay(DevelopmentUploadDelay, cancellationToken).ConfigureAwait(false);

			if (!Request.Headers.TryGetValue(FileNameHeaderName, out var fileNames) || string.IsNullOrWhiteSpace(fileNames.FirstOrDefault()))
				return BadRequest($"The filename was not found in the {FileNameHeaderName} header.");

			if (!Request.Headers.TryGetValue(UploadTypeHeaderName, out var uploadTypes))
				return BadRequest($"The file upload type must be specified using the {UploadTypeHeaderName} header.");

			if (!Enum.TryParse(uploadTypes.FirstOrDefault(), true, out TUploadType uploadType) || !IsUploadTypeAllowed(uploadType))
				return BadRequest($"The value specified in the {UploadTypeHeaderName} header is invalid.");

			string fileName = fileNames.First()!;
			string extension = Path.GetExtension(fileName);
			string tempFileName = Guid.NewGuid() + extension;
			context = new()
			{
				FileName = fileName,
				FileExtension = extension,
				ContentType = Request.Headers.TryGetValue(ContentTypeHeaderName, out var contentTypes)
					? contentTypes.FirstOrDefault() ?? "application/octet-stream" : "application/octet-stream",
				ContentLength = Request.ContentLength,
				UploadType = uploadType,
				TempFileName = tempFileName,
				TempPath = TempFileHandler.GetTempFilePath(tempFileName),
				TempUrl = TempFileHandler.GetTempWebFilePath(tempFileName)
			};

			if (context.ContentLength is 0)
				return BadRequest("The uploaded file has no content.");

			var rejection = await ValidateUploadAsync(context, cancellationToken).ConfigureAwait(false);
			if (rejection is not null)
				return rejection;

			prepared = await PrepareUploadStreamAsync(context, cancellationToken).ConfigureAwait(false);
			if (prepared.Rejection is not null)
				return prepared.Rejection;

			cancellationToken.ThrowIfCancellationRequested();
			storageAttempted = true;
			var fileInfo = await FileProvider.SaveAsync(context.TempPath, prepared.Stream!, cancellationToken: cancellationToken).ConfigureAwait(false);
			if (fileInfo.Length is 0)
				return BadRequest("The uploaded file has no content.");

			await WriteUploadMetadataAsync(context, fileInfo, cancellationToken).ConfigureAwait(false);
			var result = CreateUploadResult(context, fileInfo);
			completed = true;
			return result;
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception exception)
		{
			if (TryCreateExceptionResult(exception, out var result))
				return result;

			_ = Logger.WriteError(exception, context);
			if (IsDevelopment)
				throw;

			return InternalServerError("There has been a problem uploading your files to the server. Please try again.");
		}
		finally
		{
			if (prepared is { OwnsStream: true, Stream: not null } && !ReferenceEquals(prepared.Stream, Request.Body))
			{
				try
				{
					await prepared.Stream.DisposeAsync().ConfigureAwait(false);
				}
				catch (Exception exception)
				{
					_ = Logger.WriteError(exception);
				}
			}

			if (storageAttempted && !completed && context is not null)
			{
				using var cleanupCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
				try
				{
					_ = await FileProvider.DeleteAsync(context.TempPath, cleanupCancellation.Token).ConfigureAwait(false);
				}
				catch (Exception exception)
				{
					_ = Logger.WriteError(exception, new { context.TempPath });
				}
			}
		}
	}
}
