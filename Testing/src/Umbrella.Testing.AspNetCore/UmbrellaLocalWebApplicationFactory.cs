namespace Umbrella.Testing.AspNetCore;

/// <summary>
/// Standard local ASP.NET Core integration test factory.
/// </summary>
/// <typeparam name="TProgram">The application entry point type.</typeparam>
public abstract class UmbrellaLocalWebApplicationFactory<TProgram> : UmbrellaWebApplicationFactory<TProgram>
	where TProgram : class
{
}
