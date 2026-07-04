using Microsoft.EntityFrameworkCore;

namespace Umbrella.Testing.AspNetCore.TestApp;

public sealed class SmokeDbContext : DbContext
{
	public SmokeDbContext(DbContextOptions<SmokeDbContext> options)
		: base(options)
	{
	}

	public DbSet<SmokeEntity> Entities => Set<SmokeEntity>();
}
