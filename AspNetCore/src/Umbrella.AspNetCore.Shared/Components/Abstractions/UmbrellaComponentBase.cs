using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Umbrella.AspNetCore.Shared.Services.Abstractions;
using Umbrella.Utilities.Mapping.Abstractions;

namespace Umbrella.AspNetCore.Shared.Components.Abstractions;

/// <summary>
/// A base component to be used with Blazor components which contain commonly used functionality.
/// </summary>
/// <seealso cref="ComponentBase" />
/// <seealso cref="IAsyncDisposable"/>
public abstract class UmbrellaComponentBase : ComponentBase, IAsyncDisposable
{
	private Lazy<CancellationTokenSource>? _cancellationTokenSource;
	private bool _disposedValue;

	[Inject]
	private ILoggerFactory LoggerFactory { get; set; } = null!;

	/// <summary>
	/// Gets the navigation manager.
	/// </summary>
	/// <remarks>
	/// Useful extension methods can be found inside <see cref="NavigationManagerExtensions"/>.
	/// </remarks>
	[Inject]
	protected NavigationManager Navigation { get; private set; } = null!;

	/// <summary>
	/// Gets the mapper.
	/// </summary>
	[Inject]
	protected IUmbrellaMapper Mapper { get; private set; } = null!;

	/// <summary>
	/// Gets the HTTP request aborted service.
	/// </summary>
	/// <remarks>
	/// When this component is running on server, this service will provide data about the current HttpContext. In Blazor
	/// WebAssembly, this service will provide a no-op implementation as there is no context.
	/// </remarks>
	[Inject]
	protected IHttpContextService HttpContextService { get; private set; } = null!;

	[Inject]
	private protected IClaimsPrincipalAccessorService ClaimsPrincipalAccessorService { get; set; } = null!;

	/// <summary>
	/// Gets the logger.
	/// </summary>
	protected ILogger Logger { get; private set; } = null!;

	/// <summary>
	/// Gets the cancellation token.
	/// </summary>
	protected CancellationToken CancellationToken
	{
		get
		{
			if (_cancellationTokenSource is not null)
				return _cancellationTokenSource.Value.Token;

			_cancellationTokenSource = new Lazy<CancellationTokenSource>(() => CancellationTokenSource.CreateLinkedTokenSource(HttpContextService.RequestAborted));

			return _cancellationTokenSource.Value.Token;
		}
	}

	/// <inheritdoc />
	protected override void OnInitialized()
	{
		base.OnInitialized();

		Logger = LoggerFactory.CreateLogger(GetType());
	}

	/// <summary>
	/// Gets the claims principal for the current user.
	/// </summary>
	/// <returns>The claims principal.</returns>
	protected ValueTask<ClaimsPrincipal> GetClaimsPrincipalAsync() => ClaimsPrincipalAccessorService.GetAsync();

	/// <summary>
	/// Releases unmanaged and - optionally - managed resources.
	/// </summary>
	/// <param name="disposing">
	/// <c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.
	/// </param>
	protected virtual async ValueTask DisposeAsync(bool disposing)
	{
		if (!_disposedValue)
		{
			if (disposing)
			{
				if (_cancellationTokenSource is { IsValueCreated: true })
				{
					if (_cancellationTokenSource.Value.IsCancellationRequested)
					{
#if NET8_0_OR_GREATER
						await _cancellationTokenSource.Value.CancelAsync();
#else
						await Task.Yield();
						_cancellationTokenSource.Value.Cancel();
#endif
					}

					_cancellationTokenSource.Value.Dispose();
				}
			}

			_disposedValue = true;
		}
	}

	/// <inheritdoc/>
	public async ValueTask DisposeAsync()
	{
		await DisposeAsync(disposing: true);
		GC.SuppressFinalize(this);
	}
}