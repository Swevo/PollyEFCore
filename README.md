# PollyEFCore

[![NuGet](https://img.shields.io/nuget/v/PollyEFCore.svg)](https://www.nuget.org/packages/PollyEFCore)
[![NuGet Downloads](https://img.shields.io/nuget/dt/PollyEFCore.svg)](https://www.nuget.org/packages/PollyEFCore)
[![CI](https://github.com/Swevo/PollyEFCore/actions/workflows/build.yml/badge.svg)](https://github.com/Swevo/PollyEFCore/actions)

**Polly v8 resilience pipelines for Entity Framework Core** — automatically wrap every EF Core command (queries, `SaveChanges`, scalar operations) with retry, timeout, circuit-breaker and more. Handles transient database errors, connection blips and SQL timeouts without changing a single line of handler or repository code.

```csharp
services.AddDbContext<AppDbContext>(options =>
    options
        .UseSqlServer(connectionString)
        .AddPollyResilience(pipeline =>
            pipeline.AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(200),
                BackoffType = DelayBackoffType.Exponential,
                ShouldHandle = new PredicateBuilder().Handle<Exception>(),
            })));
```

Every query and `SaveChangesAsync()` call is now automatically retried on transient failure — no changes to your DbContext, repositories, or handlers.

---

## Why PollyEFCore?

EF Core's built-in `EnableRetryOnFailure()` only handles SQL Azure connection failures. PollyEFCore gives you the full power of Polly v8 for **any** EF Core provider.

| Feature | `EnableRetryOnFailure()` | **PollyEFCore** |
|---|---|---|
| Provider | SQL Server / Azure SQL only | **Any provider** (Postgres, MySQL, SQLite…) |
| Retry strategy | Fixed with jitter | Any Polly strategy (exponential, linear, custom) |
| Timeout | ❌ | ✅ per-command timeout |
| Circuit breaker | ❌ | ✅ stop hammering a broken DB |
| Hedging | ❌ | ✅ parallel speculative queries |
| Observability | ❌ | ✅ via `PollyOpenTelemetry` |
| Exception filter | Hardcoded SQL error codes | **Any predicate** |

---

## Installation

```bash
dotnet add package PollyEFCore
```

Targets **net8.0** and **net9.0** (requires EF Core 8+).

Dependencies: `Polly.Core 8.*`, `Microsoft.EntityFrameworkCore.Relational 8.*`

---

## Quick start

### Automatic interception (recommended)

Register once on `DbContextOptionsBuilder` — all commands are wrapped automatically:

```csharp
// Program.cs / Startup.cs
services.AddDbContext<AppDbContext>(options =>
    options
        .UseNpgsql(connectionString)           // works with any provider
        .AddPollyResilience(pipeline =>
            pipeline
                .AddRetry(new RetryStrategyOptions
                {
                    MaxRetryAttempts = 3,
                    Delay = TimeSpan.FromMilliseconds(100),
                    BackoffType = DelayBackoffType.Exponential,
                    ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                })
                .AddTimeout(TimeSpan.FromSeconds(30))));
```

Use your DbContext exactly as before — no code changes required:

```csharp
// Repository — unchanged
public async Task<List<Product>> GetProductsAsync(CancellationToken ct)
    => await _context.Products.Where(p => p.Active).ToListAsync(ct); // retried automatically

public async Task SaveAsync(Product product, CancellationToken ct)
{
    _context.Products.Add(product);
    await _context.SaveChangesAsync(ct); // retried automatically
}
```

### Explicit wrapping (for fine-grained control)

Use `ExecuteWithResilienceAsync` when you need different pipelines per operation, or when working inside an explicit transaction:

```csharp
var products = await _context.Database.ExecuteWithResilienceAsync(
    _pipeline,
    ct => _context.Products.Where(p => p.Active).ToListAsync(ct),
    cancellationToken);
```

```csharp
// Void overload for fire-and-forget style operations
await _context.Database.ExecuteWithResilienceAsync(
    _pipeline,
    async ct =>
    {
        _context.Orders.Add(order);
        await _context.SaveChangesAsync(ct);
    },
    cancellationToken);
```

---

## ASP.NET Core example

```csharp
var builder = WebApplication.CreateBuilder(args);

// Configure EF Core with Polly resilience
builder.Services.AddDbContext<ShopDbContext>(options =>
    options
        .UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
        .AddPollyResilience(pipeline =>
            pipeline
                .AddRetry(new RetryStrategyOptions
                {
                    MaxRetryAttempts = 3,
                    Delay = TimeSpan.FromMilliseconds(200),
                    BackoffType = DelayBackoffType.Exponential,
                    ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                })
                .AddTimeout(TimeSpan.FromSeconds(30))
                .AddCircuitBreaker(new CircuitBreakerStrategyOptions
                {
                    FailureRatio = 0.5,
                    MinimumThroughput = 10,
                    SamplingDuration = TimeSpan.FromSeconds(30),
                    BreakDuration = TimeSpan.FromSeconds(15),
                })));
```

---

## Combining with PollyMediatR

Use with [PollyMediatR](https://www.nuget.org/packages/PollyMediatR) for full stack resilience in CQRS apps:

```csharp
// MediatR handler resilience (outer layer)
services.AddPollyMediatR(pipeline => pipeline.AddRetry(...));

// EF Core command resilience (inner layer)
services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(cs).AddPollyResilience(pipeline => pipeline.AddRetry(...)));
```

---

## ⚠️ Transaction note

The automatic interceptor wraps individual ADO.NET commands. When using **explicit `DbTransaction`** objects, configure your pipeline to only handle connection-level failures (before command execution), or use `ExecuteWithResilienceAsync` for full unit-of-work retry control.

---

## Related packages

| Package | Downloads | Description |
|---|---|---|
| [PollyMediatR](https://www.nuget.org/packages/PollyMediatR) | [![Downloads](https://img.shields.io/nuget/dt/PollyMediatR.svg)](https://www.nuget.org/packages/PollyMediatR) | Polly v8 pipelines for MediatR request handlers |
| [PollyBackoff](https://www.nuget.org/packages/PollyBackoff) | [![Downloads](https://img.shields.io/nuget/dt/PollyBackoff.svg)](https://www.nuget.org/packages/PollyBackoff) | Jitter, linear & custom backoff for Polly v8 retry |
| [PollyChaos](https://www.nuget.org/packages/PollyChaos) | [![Downloads](https://img.shields.io/nuget/dt/PollyChaos.svg)](https://www.nuget.org/packages/PollyChaos) | Fault & latency injection (Simmy for Polly v8) |
| [PollyCaching](https://www.nuget.org/packages/PollyCaching) | [![Downloads](https://img.shields.io/nuget/dt/PollyCaching.svg)](https://www.nuget.org/packages/PollyCaching) | Cache-aside resilience strategy for Polly v8 |
| [PollyBulkhead](https://www.nuget.org/packages/PollyBulkhead) | [![Downloads](https://img.shields.io/nuget/dt/PollyBulkhead.svg)](https://www.nuget.org/packages/PollyBulkhead) | Bulkhead / concurrency limiter for Polly v8 |
| [PollyOpenTelemetry](https://www.nuget.org/packages/PollyOpenTelemetry) | [![Downloads](https://img.shields.io/nuget/dt/PollyOpenTelemetry.svg)](https://www.nuget.org/packages/PollyOpenTelemetry) | OpenTelemetry metrics & tracing for Polly v8 |

---

## License

MIT
