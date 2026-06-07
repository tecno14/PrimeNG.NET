using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PrimeNG.NET.Tests.TestEntities;

namespace PrimeNG.NET.Tests.Infrastructure;

/// <summary>
/// SQL Server LocalDB fixture for natural string sort tests.
/// Requires (localdb)\mssqllocaldb. Tests skip when the instance is unavailable.
/// </summary>
public class SqlServerTestFixture : IAsyncLifetime
{
    private readonly string _databaseName = $"PrimeNG_NET_Tests_{Guid.NewGuid():N}";
    private TestDbContext? _context;
    private bool _initialized;
    private string? _initError;

    public bool IsAvailable => _initialized;

    public string? UnavailableReason => _initError;

    public TestDbContext Context => _context
        ?? throw new InvalidOperationException("Fixture not initialized or SQL Server unavailable.");

    public async Task InitializeAsync()
    {
        var connectionString =
            Environment.GetEnvironmentVariable("PRIMENG_TEST_CONNECTION_STRING")
            ?? $"Server=(localdb)\\mssqllocaldb;Database={_databaseName};Integrated Security=True;TrustServerCertificate=True;Encrypt=False";

        try
        {
            var options = new DbContextOptionsBuilder<TestDbContext>()
                .UseSqlServer(connectionString)
                .Options;

            _context = new TestDbContext(options);
            await _context.Database.EnsureCreatedAsync();
            _context.Products.AddRange(TestData.CreateProducts());
            await _context.SaveChangesAsync();
            _initialized = true;
        }
        catch (Exception ex)
        {
            _initError = ex.Message;
            if (_context != null)
                await _context.DisposeAsync();
            _context = null;
        }
    }

    public async Task DisposeAsync()
    {
        if (_context != null && _initialized)
        {
            await _context.Database.EnsureDeletedAsync();
            await _context.DisposeAsync();
        }
    }

    public static async Task<bool> CanConnectAsync()
    {
        var connectionString =
            Environment.GetEnvironmentVariable("PRIMENG_TEST_CONNECTION_STRING")
            ?? "Server=(localdb)\\mssqllocaldb;Database=master;Integrated Security=True;TrustServerCertificate=True;Encrypt=False";

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }
}

[CollectionDefinition("SqlServerDatabase")]
public class SqlServerDatabaseCollection : ICollectionFixture<SqlServerTestFixture>;
