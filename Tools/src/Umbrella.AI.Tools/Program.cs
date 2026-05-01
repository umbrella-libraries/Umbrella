using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Umbrella.AI.Tools;
using Umbrella.AI.Tools.Services;

using IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        _ = services.AddSingleton(_ => new AiBundleInstaller(
            AiBundleAssetLocator.ResolveAssetRoot(),
            "Umbrella.AI.Tools",
            typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0"));
        _ = services.AddSingleton<Main>();
    })
    .Build();

var main = host.Services.GetRequiredService<Main>();
return await main.ExecuteAsync(args);
