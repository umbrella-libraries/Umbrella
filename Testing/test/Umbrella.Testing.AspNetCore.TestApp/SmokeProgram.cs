using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Umbrella.Testing.AspNetCore.TestApp;

public sealed class SmokeProgram
{
	public static void Main(string[] args) => CreateHostBuilder(args).Build().Run();

	public static IHostBuilder CreateHostBuilder(string[] args) =>
		Host.CreateDefaultBuilder(args)
			.ConfigureWebHostDefaults(builder =>
			{
				_ = builder.Configure(app =>
				{
					app.Run(async context =>
					{
						if (context.Request.Path == "/db")
						{
							SmokeDbContext dbContext = context.RequestServices.GetRequiredService<SmokeDbContext>();

							_ = await dbContext.Entities.AddAsync(new SmokeEntity { Name = "Created" }, context.RequestAborted);
							_ = await dbContext.SaveChangesAsync(context.RequestAborted);
							int entityCount = await dbContext.Entities.CountAsync(context.RequestAborted);

							await context.Response.WriteAsync(entityCount.ToString(CultureInfo.InvariantCulture), context.RequestAborted);

							return;
						}

						IHostEnvironment environment = context.RequestServices.GetRequiredService<IHostEnvironment>();

						await context.Response.WriteAsync(environment.EnvironmentName, context.RequestAborted);
					});
				});
			});
}
