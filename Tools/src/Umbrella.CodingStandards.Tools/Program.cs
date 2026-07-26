using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Umbrella.CodingStandards.Tools;

using IHost host = Host.CreateDefaultBuilder(args)
	.ConfigureServices(services =>
	{
		_ = services.AddSingleton<Main>();
	})
	.Build();

var main = host.Services.GetRequiredService<Main>();
return await main.ExecuteAsync(args);
