using System.Net.Http;
using System.Reflection;
using Moq;
using Umbrella.AspNetCore.Blazor.Infrastructure;
using Umbrella.AspNetCore.Shared.Components.Abstractions;
using Umbrella.AspNetCore.Shared.Services.Abstractions;

namespace Umbrella.AspNetCore.Blazor.Test.Infrastructure;

public sealed class UmbrellaComponentBaseTest
{
	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(2)]
	public async Task Disposal_cancels_an_in_flight_request_and_preserves_the_cancelled_token(int baseType)
	{
		using var requestAborted = new CancellationTokenSource();
		await using IComponentProbe component = CreateComponent(baseType, requestAborted.Token);
		CancellationToken token = component.Token;
		using var handler = new PendingRequestHandler();
		using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
		Task<HttpResponseMessage> request = client.GetAsync("https://localhost/pending", token);
		await handler.Started.Task.WaitAsync(TestContext.Current.CancellationToken);

		await component.DisposeAsync();

		Assert.True(token.IsCancellationRequested);
		Assert.False(requestAborted.IsCancellationRequested);
		Assert.Equal(token, component.Token);
		_ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request.WaitAsync(TestContext.Current.CancellationToken));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(2)]
	public async Task Request_abort_cancels_the_component_token_and_disposal_remains_safe(int baseType)
	{
		using var requestAborted = new CancellationTokenSource();
		await using IComponentProbe component = CreateComponent(baseType, requestAborted.Token);
		CancellationToken token = component.Token;
		await requestAborted.CancelAsync();
		Assert.True(token.IsCancellationRequested);
		await component.DisposeAsync();
		Assert.Equal(token, component.Token);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(2)]
	public async Task Disposal_before_token_access_does_not_create_a_new_active_lifetime(int baseType)
	{
		await using IComponentProbe component = CreateComponent(baseType, TestContext.Current.CancellationToken);
		await component.DisposeAsync();
		Assert.True(component.Token.IsCancellationRequested);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(2)]
	public async Task Repeated_disposal_cancels_callbacks_only_once(int baseType)
	{
		await using IComponentProbe component = CreateComponent(baseType, TestContext.Current.CancellationToken);
		int cancellationCount = 0;
		using CancellationTokenRegistration registration = component.Token.Register(() => cancellationCount++);
		await component.DisposeAsync();
		await component.DisposeAsync();
		Assert.Equal(1, cancellationCount);
	}

	[Fact]
	public async Task A_failing_cancellation_callback_does_not_leave_disposal_incomplete()
	{
		await using IComponentProbe component = CreateComponent(0, TestContext.Current.CancellationToken);
		CancellationToken token = component.Token;
		using CancellationTokenRegistration registration = token.Register(() => throw new InvalidOperationException("Callback failure"));
		_ = await Assert.ThrowsAsync<AggregateException>(() => component.DisposeAsync().AsTask());
		Assert.True(token.IsCancellationRequested);
		Assert.Equal(token, component.Token);
		await component.DisposeAsync();
	}

	private static IComponentProbe CreateComponent(int baseType, CancellationToken requestAborted)
	{
		IComponentProbe component = baseType switch
		{
			0 => new ComponentProbe(),
			1 => new ClientComponentProbe(),
			2 => new DialogComponentProbe(),
			_ => throw new ArgumentOutOfRangeException(nameof(baseType))
		};
		var context = new Mock<IHttpContextService>();
		_ = context.SetupGet(x => x.RequestAborted).Returns(requestAborted);
		typeof(UmbrellaComponentBase).GetProperty("HttpContextService", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(component, context.Object);
		return component;
	}

	private interface IComponentProbe : IAsyncDisposable
	{
		CancellationToken Token { get; }
	}

	private sealed class ComponentProbe : UmbrellaComponentBase, IComponentProbe
	{
		public CancellationToken Token => CancellationToken;
	}

	private sealed class ClientComponentProbe : UmbrellaClientComponentBase, IComponentProbe
	{
		public CancellationToken Token => CancellationToken;
	}

	private sealed class DialogComponentProbe : UmbrellaDialogComponentBase, IComponentProbe
	{
		public CancellationToken Token => CancellationToken;
	}

	private sealed class PendingRequestHandler : HttpMessageHandler
	{
		internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			_ = Started.TrySetResult();
			await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
			return new(HttpStatusCode.OK);
		}
	}
}