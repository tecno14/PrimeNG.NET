using Microsoft.EntityFrameworkCore;

namespace PrimeNG.NET.Tests.TestEntities;

public class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    public DbSet<TestProduct> Products => Set<TestProduct>();
}
