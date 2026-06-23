namespace PollyEFCore.Tests;

// ── DatabaseFacadeExtensions tests ────────────────────────────────────────────
// These are pure unit tests — no real database needed.

public class DatabaseFacadeExtensionsTests
{
    private static Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade GetFacade()
    {
        var opts = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        var ctx = new TestDbContext(opts);
        return ctx.Database;
    }

    // ── Generic overload ──────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteWithResilienceAsync_Generic_ReturnsOperationResult()
    {
        var db = GetFacade();
        var result = await db.ExecuteWithResilienceAsync(
            ResiliencePipeline.Empty,
            ct => Task.FromResult(42));

        result.Should().Be(42);
    }

    [Fact]
    public async Task ExecuteWithResilienceAsync_Generic_RetriesOnFault()
    {
        var db = GetFacade();
        var calls = 0;
        var pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.Zero,
                ShouldHandle = new PredicateBuilder().Handle<Exception>(),
            })
            .Build();

        var result = await db.ExecuteWithResilienceAsync(pipeline, ct =>
        {
            if (++calls <= 2) throw new InvalidOperationException("transient");
            return Task.FromResult("recovered");
        });

        result.Should().Be("recovered");
        calls.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteWithResilienceAsync_Generic_ThrowsWhenRetriesExhausted()
    {
        var db = GetFacade();
        var pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 1,
                Delay = TimeSpan.Zero,
                ShouldHandle = new PredicateBuilder().Handle<Exception>(),
            })
            .Build();

        var act = async () => await db.ExecuteWithResilienceAsync(pipeline, ct =>
            Task.FromException<string>(new InvalidOperationException("always fails")));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ExecuteWithResilienceAsync_Generic_ForwardsCancellationToken()
    {
        var db = GetFacade();
        using var cts = new CancellationTokenSource();
        CancellationToken captured = default;

        await db.ExecuteWithResilienceAsync(ResiliencePipeline.Empty, ct =>
        {
            captured = ct;
            return Task.FromResult(0);
        }, cts.Token);

        captured.Should().Be(cts.Token);
    }

    // ── Non-generic overload ──────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteWithResilienceAsync_Void_CompletesSuccessfully()
    {
        var db = GetFacade();
        var executed = false;

        await db.ExecuteWithResilienceAsync(ResiliencePipeline.Empty, ct =>
        {
            executed = true;
            return Task.CompletedTask;
        });

        executed.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteWithResilienceAsync_Void_RetriesOnFault()
    {
        var db = GetFacade();
        var calls = 0;
        var pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.Zero,
                ShouldHandle = new PredicateBuilder().Handle<Exception>(),
            })
            .Build();

        await db.ExecuteWithResilienceAsync(pipeline, ct =>
        {
            if (++calls <= 2) throw new InvalidOperationException("transient");
            return Task.CompletedTask;
        });

        calls.Should().Be(3);
    }

    // ── Timeout ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteWithResilienceAsync_Timeout_CancelsSlowOperation()
    {
        var db = GetFacade();
        var pipeline = new ResiliencePipelineBuilder()
            .AddTimeout(TimeSpan.FromMilliseconds(50))
            .Build();

        var act = async () => await db.ExecuteWithResilienceAsync(pipeline, async ct =>
        {
            await Task.Delay(TimeSpan.FromSeconds(10), ct);
            return 0;
        });

        await act.Should().ThrowAsync<TimeoutRejectedException>();
    }

    // ── Null guards ───────────────────────────────────────────────────────────

    [Fact]
    public void ExecuteWithResilienceAsync_Generic_NullDatabase_Throws()
    {
        Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade db = null!;
        Action act = () => db.ExecuteWithResilienceAsync(ResiliencePipeline.Empty, ct => Task.FromResult(0));
        act.Should().Throw<ArgumentNullException>().WithParameterName("database");
    }

    [Fact]
    public void ExecuteWithResilienceAsync_Generic_NullPipeline_Throws()
    {
        var db = GetFacade();
        Action act = () => db.ExecuteWithResilienceAsync(null!, ct => Task.FromResult(0));
        act.Should().Throw<ArgumentNullException>().WithParameterName("pipeline");
    }

    [Fact]
    public void ExecuteWithResilienceAsync_Generic_NullOperation_Throws()
    {
        var db = GetFacade();
        Func<CancellationToken, Task<int>> op = null!;
        Action act = () => db.ExecuteWithResilienceAsync(ResiliencePipeline.Empty, op);
        act.Should().Throw<ArgumentNullException>().WithParameterName("operation");
    }

    [Fact]
    public void ExecuteWithResilienceAsync_Void_NullDatabase_Throws()
    {
        Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade db = null!;
        Action act = () => db.ExecuteWithResilienceAsync(ResiliencePipeline.Empty, ct => Task.CompletedTask);
        act.Should().Throw<ArgumentNullException>().WithParameterName("database");
    }

    [Fact]
    public void ExecuteWithResilienceAsync_Void_NullPipeline_Throws()
    {
        var db = GetFacade();
        Action act = () => db.ExecuteWithResilienceAsync(null!, ct => Task.CompletedTask);
        act.Should().Throw<ArgumentNullException>().WithParameterName("pipeline");
    }

    [Fact]
    public void ExecuteWithResilienceAsync_Void_NullOperation_Throws()
    {
        var db = GetFacade();
        Func<CancellationToken, Task> op = null!;
        Action act = () => db.ExecuteWithResilienceAsync(ResiliencePipeline.Empty, op);
        act.Should().Throw<ArgumentNullException>().WithParameterName("operation");
    }
}
