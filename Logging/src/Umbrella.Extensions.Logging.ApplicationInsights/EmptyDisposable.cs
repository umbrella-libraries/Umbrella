namespace Umbrella.Extensions.Logging.ApplicationInsights;

/// <summary>
/// A dummy <see cref="IDisposable"/> that does nothing.
/// </summary>
/// <seealso cref="IDisposable" />
public sealed class EmptyDisposable : IDisposable
{
	/// <summary>
	/// Gets the instance.
	/// </summary>
	public static IDisposable Instance { get; } = new EmptyDisposable();

	/// <inheritdoc />
	public void Dispose() => GC.SuppressFinalize(this);
}