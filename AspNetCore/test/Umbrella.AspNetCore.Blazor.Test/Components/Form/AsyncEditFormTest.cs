using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Moq;
using Umbrella.AspNetCore.Blazor.Components.Form;
using Umbrella.AspNetCore.Blazor.Components.Validation;
using Umbrella.Utilities.DataAnnotations;
using Umbrella.Utilities.DataAnnotations.Abstractions;

namespace Umbrella.AspNetCore.Blazor.Test.Components.Form;

[SuppressMessage("Usage", "VSTHRD003:Avoid awaiting foreign Tasks", Justification = "Test-owned completion sources control validation order without timing-dependent delays.")]
public sealed class AsyncEditFormTest
{
#if NET11_0_OR_GREATER
	[Theory]
	[InlineData(true, true, true)]
	[InlineData(true, true, false)]
	[InlineData(true, false, true)]
	[InlineData(true, false, false)]
	[InlineData(false, true, true)]
	[InlineData(false, true, false)]
	[InlineData(false, false, true)]
	[InlineData(false, false, false)]
	public async Task Submit_waits_for_both_validators_and_invokes_the_correct_callback_once(bool graphFinishesFirst, bool graphValid, bool nativeValid)
	{
		var context = new EditContext(new object());
		var graphResult = new TaskCompletionSource<(bool, IReadOnlyCollection<ObjectGraphValidationResult>)>(TaskCreationOptions.RunContinuationsAsynchronously);
		using var graph = new ValidatorProbe(context, graphResult.Task);
		var graphCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		graph.OnObjectValidationCompleted += (_, _) => graphCompleted.SetResult();
		var nativeResult = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var nativeCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var messages = new ValidationMessageStore(context);
		context.OnValidationRequested += (_, args) => args.AddAsyncValidator(async token =>
		{
			await nativeResult.Task.WaitAsync(token);
			if (!nativeValid)
				messages.Add(new FieldIdentifier(context.Model, "Native"), "Native validation failed.");
			nativeCompleted.SetResult();
		});

		int validCalls = 0;
		int invalidCalls = 0;
		using var form = new FormProbe(context, _ => validCalls++, _ => invalidCalls++);
		Task submit = form.SubmitAsync();

		Assert.False(submit.IsCompleted);
		Assert.Equal(0, validCalls + invalidCalls);

		var results = CreateResults(context.Model, graphValid);
		if (graphFinishesFirst)
		{
			graphResult.SetResult((graphValid, results));
			await WaitForAsync(graphCompleted.Task);
		}
		else
		{
			nativeResult.SetResult();
			await WaitForAsync(nativeCompleted.Task);
		}

		Assert.False(submit.IsCompleted);
		Assert.Equal(0, validCalls + invalidCalls);

		if (graphFinishesFirst)
			nativeResult.SetResult();
		else
			graphResult.SetResult((graphValid, results));

		await WaitForAsync(submit);

		bool expectedValid = graphValid && nativeValid;
		Assert.Equal(expectedValid ? 1 : 0, validCalls);
		Assert.Equal(expectedValid ? 0 : 1, invalidCalls);
		Assert.Equal((graphValid ? 0 : 1) + (nativeValid ? 0 : 1), context.GetValidationMessages().Count());
	}

	[Fact]
	public async Task A_faulted_async_validator_invokes_invalid_submit_even_without_validation_messages()
	{
		var context = new EditContext(new object());
		using var graph = new ValidatorProbe(context, Task.FromResult((true, CreateResults(context.Model, true))));
		context.OnValidationRequested += (_, args) => args.AddAsyncValidator(_ => Task.FromException(new InvalidOperationException("Validation failed.")));
		int validCalls = 0;
		int invalidCalls = 0;
		using var form = new FormProbe(context, _ => validCalls++, _ => invalidCalls++);

		await WaitForAsync(form.SubmitAsync());

		Assert.Empty(context.GetValidationMessages());
		Assert.True(context.IsValidationFaulted());
		Assert.Equal(0, validCalls);
		Assert.Equal(1, invalidCalls);
	}

	[Fact]
	public async Task Async_validation_passes_its_cancellation_token_to_the_graph_validator()
	{
		var context = new EditContext(new object());
		using var graph = new ValidatorProbe(context, Task.FromResult((true, CreateResults(context.Model, true))));
		using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

		Assert.True(await context.ValidateAsync(cancellation.Token));
		graph.Validator.Verify(x => x.TryValidateObjectAsync(context.Model, It.IsAny<ValidationContext>(), true, It.IsAny<IServiceProvider>(), cancellation.Token), Times.Once);
	}
#else
	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task Legacy_submit_still_waits_for_graph_completion_to_invoke_its_callback(bool valid)
	{
		var context = new EditContext(new object());
		var graphResult = new TaskCompletionSource<(bool, IReadOnlyCollection<ObjectGraphValidationResult>)>(TaskCreationOptions.RunContinuationsAsynchronously);
		using var graph = new ValidatorProbe(context, graphResult.Task);
		int validCalls = 0;
		int invalidCalls = 0;
		var callbackCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		using var form = new FormProbe(context,
			_ =>
			{
				validCalls++;
				callbackCompleted.SetResult();
			},
			_ =>
			{
				invalidCalls++;
				callbackCompleted.SetResult();
			});

		await form.SubmitAsync();
		Assert.Equal(0, validCalls + invalidCalls);
		graphResult.SetResult((valid, CreateResults(context.Model, valid)));
		await WaitForAsync(callbackCompleted.Task);

		Assert.Equal(valid ? 1 : 0, validCalls);
		Assert.Equal(valid ? 0 : 1, invalidCalls);
	}

	[Fact]
	public void Synchronous_validation_still_reports_graph_results()
	{
		var context = new EditContext(new object());
		using var graph = new ValidatorProbe(context, Task.FromResult((false, CreateResults(context.Model, false))));

		Assert.False(context.Validate());
		_ = Assert.Single(context.GetValidationMessages());
	}
#endif

	[Fact]
	public async Task Explicit_submit_handler_controls_validation()
	{
		var context = new EditContext(new object());
		int validations = 0;
		context.OnValidationRequested += (_, _) => validations++;
		using var form = new FormProbe(context, _ => Assert.Fail("Unexpected valid submit."), _ => Assert.Fail("Unexpected invalid submit."));
		int submits = 0;
		form.SetSubmitHandler(_ => submits++);

		await WaitForAsync(form.SubmitAsync());

		Assert.Equal(1, submits);
		Assert.Equal(0, validations);
	}

	private static IReadOnlyCollection<ObjectGraphValidationResult> CreateResults(object model, bool valid)
		=> valid ? [] : [new ObjectGraphValidationResult(new ValidationResult("Graph validation failed.", ["Graph"]), model)];

	private static async Task WaitForAsync(Task task)
		=> await task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

	private sealed class FormProbe : AsyncEditForm
	{
		internal FormProbe(EditContext context, Action<EditContext> valid, Action<EditContext> invalid)
		{
			EditContext = context;
			OnValidSubmit = EventCallback.Factory.Create(new object(), valid);
			OnInvalidSubmit = EventCallback.Factory.Create(new object(), invalid);
			OnAfterRender(true);
		}

		internal void SetSubmitHandler(Action<EditContext> handler)
		{
			OnValidSubmit = default;
			OnInvalidSubmit = default;
			OnSubmit = EventCallback.Factory.Create(new object(), handler);
		}

		internal Task SubmitAsync()
		{
			// Exercise the delegate that EditForm binds to the form's submit event.
			var submit = (Func<Task>)typeof(EditForm).GetField("_handleSubmitDelegate", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(this)!;
			return submit();
		}
	}

	private sealed class ValidatorProbe : ObjectGraphDataAnnotationsValidator
	{
		internal Mock<IObjectGraphValidator> Validator { get; } = new();

		internal ValidatorProbe(EditContext context, Task<(bool, IReadOnlyCollection<ObjectGraphValidationResult>)> result)
		{
			_ = Validator.Setup(x => x.TryValidateObjectAsync(It.IsAny<object>(), It.IsAny<ValidationContext>(), true, It.IsAny<IServiceProvider>(), It.IsAny<CancellationToken>())).Returns(result);
			SetProperty("EditContext", context);
			SetProperty("ObjectGraphValidator", Validator.Object);
			SetProperty("ServiceProvider", Mock.Of<IServiceProvider>());
			OnInitialized();
		}

		private void SetProperty(string name, object value)
			=> typeof(ObjectGraphDataAnnotationsValidator).GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(this, value);
	}
}
