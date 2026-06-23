namespace PollyEFCore.Tests;

// ── Test entities & DbContext ─────────────────────────────────────────────────

public class Widget
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public class TestDbContext : DbContext
{
    public DbSet<Widget> Widgets => Set<Widget>();

    public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }
}

// ── Factory helpers ───────────────────────────────────────────────────────────

public static class TestContextFactory
{
    /// <summary>
    /// Creates a DbContext backed by an in-memory SQLite database via a persistent
    /// open connection (required so the in-memory DB survives between EF round-trips).
    /// The caller must dispose both the context AND the returned connection.
    /// </summary>
    public static (TestDbContext Context, Microsoft.Data.Sqlite.SqliteConnection Connection) Create(
        Action<DbContextOptionsBuilder<TestDbContext>>? configure = null)
    {
        var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        connection.Open();

        var builder = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(connection);

        configure?.Invoke(builder);

        var ctx = new TestDbContext(builder.Options);
        ctx.Database.EnsureCreated();
        return (ctx, connection);
    }

    public static (TestDbContext Context, Microsoft.Data.Sqlite.SqliteConnection Connection)
        CreateWithResilience(ResiliencePipeline pipeline)
        => Create(b => b.AddPollyResilience(pipeline));
}
