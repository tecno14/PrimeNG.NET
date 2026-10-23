using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using PrimeNG.NET.Tests.TestEntities;

namespace PrimeNG.NET.Tests.Infrastructure;

/// <summary>
/// PostgreSQL fixture for natural string sort tests.
/// Set <c>PRIMENG_TEST_PG_CONNECTION_STRING</c>. Uses an isolated schema in that database
/// (no CREATEDB privilege required). Tests skip when the server is unavailable.
/// </summary>
public class PostgresTestFixture : IAsyncLifetime
{
    private readonly string _schemaName = $"primeng_net_{Guid.NewGuid():N}";
    private SchemaTestDbContext? _context;
    private bool _initialized;
    private string? _initError;
    private string? _connectionString;

    public bool IsAvailable => _initialized;

    public string? UnavailableReason => _initError;

    public TestDbContext Context => _context
        ?? throw new InvalidOperationException("Fixture not initialized or PostgreSQL unavailable.");

    public async Task InitializeAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("PRIMENG_TEST_PG_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _initError = "PRIMENG_TEST_PG_CONNECTION_STRING is not set.";
            return;
        }

        _connectionString = connectionString;

        try
        {
            await using (var conn = new NpgsqlConnection(connectionString))
            {
                await conn.OpenAsync();
                await using var createSchema = new NpgsqlCommand(
                    $"CREATE SCHEMA \"{_schemaName}\"",
                    conn);
                await createSchema.ExecuteNonQueryAsync();
            }

            var options = new DbContextOptionsBuilder<TestDbContext>()
                .UseNpgsql(connectionString)
                .Options;

            _context = new SchemaTestDbContext(options, _schemaName);
            // EnsureCreated is a no-op when the target database already has tables (e.g. app DB).
            var creator = _context.GetService<IRelationalDatabaseCreator>();
            await creator.CreateTablesAsync();

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
            await TryDropSchemaAsync();
        }
    }

    public async Task DisposeAsync()
    {
        if (_context != null)
        {
            await _context.DisposeAsync();
            _context = null;
        }

        await TryDropSchemaAsync();
    }

    private async Task TryDropSchemaAsync()
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
            return;

        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var drop = new NpgsqlCommand(
                $"DROP SCHEMA IF EXISTS \"{_schemaName}\" CASCADE",
                conn);
            await drop.ExecuteNonQueryAsync();
        }
        catch
        {
            // best-effort cleanup
        }
    }

    private sealed class SchemaTestDbContext(DbContextOptions<TestDbContext> options, string schema)
        : TestDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema(schema);
            base.OnModelCreating(modelBuilder);
        }
    }
}

[CollectionDefinition("PostgresDatabase")]
public class PostgresDatabaseCollection : ICollectionFixture<PostgresTestFixture>;
