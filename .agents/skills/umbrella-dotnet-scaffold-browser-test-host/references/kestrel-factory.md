# Kestrel mode in the factory

Use this `CreateHost` override when the built-in `WebApplicationFactory.UseKestrel()`/`StartServer()` (Microsoft.AspNetCore.Mvc.Testing 10+) is unavailable or does not work with the app. Add the members to the shared factory deriving from `UmbrellaWebApplicationFactory<TProgram>`.

The switch is `ServeOverKestrel`, not `UseKestrel`: on .NET 10 a `UseKestrel` property hides the base class's public `UseKestrel()` overloads (CS0108). Do not call the built-in `UseKestrel()` or `StartServer()` on a factory that uses this override; the two mechanisms each build their own host. Rename an existing `UseKestrel` property (for example from a .NET 8 factory) when moving to .NET 10.

```csharp
private IHost? _kestrelHost;

/// <summary>Gets the address of the real Kestrel server when <see cref="ServeOverKestrel"/> is enabled.</summary>
public Uri ServerAddress { get; private set; } = null!;

/// <summary>Gets the address the app is served from. Seed it as the app's base URL.</summary>
public Uri SiteUrl => ServeOverKestrel ? ServerAddress : new Uri("http://localhost/");

/// <summary>
/// Gets the app's services. Use these rather than Services, which belongs to an empty placeholder host in Kestrel mode.
/// </summary>
public IServiceProvider HostServices
{
	get
	{
		// Reading Services builds and starts the host, which also creates the Kestrel host when it is enabled.
		IServiceProvider services = Services;
		return _kestrelHost?.Services ?? services;
	}
}

/// <summary>
/// Gets a value indicating whether the app is served by Kestrel on a loopback port instead of TestServer.
/// The base CreateClient overloads then reach the placeholder, so use <see cref="CreateSiteClient"/>.
/// </summary>
protected virtual bool ServeOverKestrel => false;

[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The HttpClient owns and disposes its handler.")]
public HttpClient CreateSiteClient()
	=> ServeOverKestrel
		? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }, disposeHandler: true) { BaseAddress = ServerAddress }
		: CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

protected override IHost CreateHost(IHostBuilder builder)
{
	if (!ServeOverKestrel)
		return base.CreateHost(builder);

	ArgumentNullException.ThrowIfNull(builder);

	// Build the app's builder only once: apps with process-wide state fail to initialize a second host.
	_ = builder.ConfigureWebHost(webHostBuilder => webHostBuilder.UseKestrel().UseUrls("http://127.0.0.1:0"));

	_kestrelHost = builder.Build();
	_kestrelHost.Start();

	string address = _kestrelHost.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses.FirstOrDefault()
		?? throw new InvalidOperationException("The Kestrel server did not report the address it is listening on.");
	ServerAddress = new Uri(address);

	// Only when deriving from UmbrellaSqlServerAzuriteWebApplicationFactory: its CreateHost runs the migrations after
	// base.CreateHost, and this branch does not call it, so migrate the Kestrel host's database here.
	InitializeDatabase(_kestrelHost.Services);

	// WebApplicationFactory insists its host runs on TestServer, so give it an empty one. Nothing is served by it.
	IHost placeholderHost = new HostBuilder()
		.ConfigureWebHost(webHostBuilder => webHostBuilder.UseTestServer().Configure(_ => { }))
		.Build();
	placeholderHost.Start();

	return placeholderHost;
}

public override async ValueTask DisposeAsync()
{
	try
	{
		if (_kestrelHost is not null)
		{
			await _kestrelHost.StopAsync();
			_kestrelHost.Dispose();
		}

		await base.DisposeAsync();
	}
	catch (ObjectDisposedException)
	{
		// The host was already torn down after a failed start; containers and stubs still need disposing.
	}

	GC.SuppressFinalize(this);
}
```

Notes:

- Remove the `InitializeDatabase` call when the factory derives from `UmbrellaWebApplicationFactory<TProgram>` directly. A project-specific container factory that migrates or seeds in its own `CreateHost` needs that work repeated in the Kestrel branch, on `_kestrelHost.Services`.
- Seed data that stores absolute URLs (site or tenant definitions) with `SiteUrl` after the host starts, so links and redirects point at the Kestrel port.
- Test helpers that resolve services (URL resolvers, repositories) must use `HostServices`.
- If the factory already overrides `DisposeAsync` for containers or stubs, merge the Kestrel shutdown into it rather than adding a second override.
