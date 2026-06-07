using Microsoft.EntityFrameworkCore;
using PrimeNG.NET.Tests.TestEntities;

namespace PrimeNG.NET.Tests.Infrastructure;

public class SqliteTestFixture : IAsyncLifetime
{
    private TestDbContext? _context;

    public TestDbContext Context => _context
        ?? throw new InvalidOperationException("Fixture not initialized. Call InitializeAsync first.");

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite($"Data Source=primeng_tests_{Guid.NewGuid():N};Mode=Memory;Cache=Shared")
            .Options;

        _context = new TestDbContext(options);
        await _context.Database.OpenConnectionAsync();
        await _context.Database.EnsureCreatedAsync();
        _context.Products.AddRange(TestData.CreateProducts());
        await _context.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        if (_context != null)
            await _context.DisposeAsync();
    }
}

[CollectionDefinition("SqliteDatabase")]
public class SqliteDatabaseCollection : ICollectionFixture<SqliteTestFixture>;
