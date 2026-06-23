namespace PollyEFCore.Tests;

// ── ResilienceDbCommandInterceptor integration tests (SQLite) ─────────────────

public class ResilienceDbCommandInterceptorTests : IDisposable
{
    private readonly TestDbContext _context;
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;

    public ResilienceDbCommandInterceptorTests()
    {
        (_context, _connection) = TestContextFactory.CreateWithResilience(ResiliencePipeline.Empty);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Query_WithEmptyPipeline_ReturnsResults()
    {
        _context.Widgets.Add(new Widget { Name = "Sprocket" });
        await _context.SaveChangesAsync();

        var widgets = await _context.Widgets.ToListAsync();

        widgets.Should().ContainSingle(w => w.Name == "Sprocket");
    }

    [Fact]
    public async Task SaveChanges_WithEmptyPipeline_PersistsEntity()
    {
        _context.Widgets.Add(new Widget { Name = "Cog" });
        await _context.SaveChangesAsync();

        var count = await _context.Widgets.CountAsync();
        count.Should().Be(1);
    }

    [Fact]
    public async Task Query_WithEmptyPipeline_ReturnsEmpty_WhenNoData()
    {
        var widgets = await _context.Widgets.ToListAsync();
        widgets.Should().BeEmpty();
    }

    [Fact]
    public async Task Scalar_CountAsync_WithEmptyPipeline_ReturnsCount()
    {
        _context.Widgets.Add(new Widget { Name = "A" });
        _context.Widgets.Add(new Widget { Name = "B" });
        await _context.SaveChangesAsync();

        var count = await _context.Widgets.CountAsync();
        count.Should().Be(2);
    }

    [Fact]
    public async Task Query_MultipleEntities_ReturnsAll()
    {
        _context.Widgets.AddRange(
            new Widget { Name = "Alpha" },
            new Widget { Name = "Beta" },
            new Widget { Name = "Gamma" });
        await _context.SaveChangesAsync();

        var widgets = await _context.Widgets.OrderBy(w => w.Name).ToListAsync();
        widgets.Select(w => w.Name).Should().Equal("Alpha", "Beta", "Gamma");
    }
}

// ── DbContextOptionsBuilderExtensions tests ───────────────────────────────────

public class DbContextOptionsBuilderExtensionsTests : IDisposable
{
    private readonly List<IDisposable> _disposables = new();

    public void Dispose()
    {
        foreach (var d in _disposables) d.Dispose();
    }

    private (TestDbContext ctx, Microsoft.Data.Sqlite.SqliteConnection conn) Tracked(
        Action<DbContextOptionsBuilder<TestDbContext>> configure)
    {
        var result = TestContextFactory.Create(configure);
        _disposables.Add(result.Context);
        _disposables.Add(result.Connection);
        return result;
    }

    [Fact]
    public async Task AddPollyResilience_WithBuilder_RegistersInterceptor()
    {
        var (ctx, _) = Tracked(b =>
            b.AddPollyResilience(p =>
                p.AddRetry(new RetryStrategyOptions
                {
                    MaxRetryAttempts = 1,
                    ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                })));

        ctx.Widgets.Add(new Widget { Name = "Test" });
        await ctx.SaveChangesAsync();

        var result = await ctx.Widgets.ToListAsync();
        result.Should().ContainSingle();
    }

    [Fact]
    public async Task AddPollyResilience_WithPipeline_RegistersInterceptor()
    {
        var pipeline = new ResiliencePipelineBuilder().Build();
        var (ctx, _) = Tracked(b => b.AddPollyResilience(pipeline));

        ctx.Widgets.Add(new Widget { Name = "Test" });
        await ctx.SaveChangesAsync();

        var result = await ctx.Widgets.ToListAsync();
        result.Should().ContainSingle();
    }

    [Fact]
    public void AddPollyResilience_NullOptionsBuilder_Throws()
    {
        DbContextOptionsBuilder builder = null!;
        Action act = () => builder.AddPollyResilience(_ => { });
        act.Should().Throw<ArgumentNullException>().WithParameterName("optionsBuilder");
    }

    [Fact]
    public void AddPollyResilience_NullConfigure_Throws()
    {
        var builder = new DbContextOptionsBuilder();
        Action<ResiliencePipelineBuilder> configure = null!;
        Action act = () => builder.AddPollyResilience(configure);
        act.Should().Throw<ArgumentNullException>().WithParameterName("configure");
    }

    [Fact]
    public void AddPollyResilience_NullPipeline_Throws()
    {
        var builder = new DbContextOptionsBuilder();
        Action act = () => builder.AddPollyResilience((ResiliencePipeline)null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("pipeline");
    }
}

// ── ResilienceDbCommandInterceptor constructor tests ──────────────────────────

public class ResilienceDbCommandInterceptorConstructorTests
{
    [Fact]
    public void Constructor_NullPipeline_Throws()
    {
        Action act = () => new ResilienceDbCommandInterceptor(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("pipeline");
    }

    [Fact]
    public void Constructor_ValidPipeline_DoesNotThrow()
    {
        Action act = () => new ResilienceDbCommandInterceptor(ResiliencePipeline.Empty);
        act.Should().NotThrow();
    }
}
