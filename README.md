# PollyEFCore

[![NuGet](https://img.shields.io/nuget/v/PollyEFCore.svg)](https://www.nuget.org/packages/PollyEFCore)
[![NuGet Downloads](https://img.shields.io/nuget/dt/PollyEFCore.svg)](https://www.nuget.org/packages/PollyEFCore)
[![CI](https://github.com/Swevo/PollyEFCore/actions/workflows/build.yml/badge.svg)](https://github.com/Swevo/PollyEFCore/actions)

**Polly v8 resilience pipelines for Entity Framework Core** — retry, timeout and circuit-breaker for every EF Core query and `SaveChanges`, for any database provider, with a single line of registration.

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

Every query and `SaveChangesAsync()` is now automatically retried on transient failure — **no changes to your DbContext, repositories, or handlers**.

---

## Why PollyEFCore?

EF Core's built-in `EnableRetryOnFailure()` only handles SQL Azure connection failures. PollyEFCore gives you the full power of Polly v8 for **any** EF Core provider.

| Feature | `EnableRetryOnFailure()` | **PollyEFCore** |
|---|---|---|
| Provider support | SQL Server / Azure SQL only | **Any** (Postgres, MySQL, SQLite, CosmosDB…) |
| Retry strategy | Fixed with jitter, hardcoded delays | Exponential, linear, constant, custom |
| Timeout per command | ❌ | ✅ |
| Circuit breaker | ❌ | ✅ stop hammering a broken DB |
| Hedging (speculative queries) | ❌ | ✅ |
| Exception filter | Hardcoded SQL error codes | **Any predicate — your codes, your rules** |
| Observability | ❌ | ✅ via [PollyOpenTelemetry](https://www.nuget.org/packages/PollyOpenTelemetry) |

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

One call on `DbContextOptionsBuilder` — **all commands wrapped automatically**, no handler changes needed:

```csharp
services.AddDbContext<AppDbContext>(options =>
    options
        .UseNpgsql(connectionString)           // any provider
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

Use your DbContext exactly as normal — zero code changes required:

```csharp
// Repository — completely unchanged
public async Task<List<Product>> GetProductsAsync(CancellationToken ct)
    => await _context.Products.Where(p => p.Active).ToListAsync(ct); // retried automatically

public async Task SaveAsync(Product product, CancellationToken ct)
{
    _context.Products.Add(product);
    await _context.SaveChangesAsync(ct); // retried automatically
}
```

### Explicit wrapping (per-operation control)

Use `ExecuteWithResilienceAsync` when you need a different pipeline per operation, or when working inside an explicit transaction:

```csharp
var products = await _context.Database.ExecuteWithResilienceAsync(
    _pipeline,
    ct => _context.Products.Where(p => p.Active).ToListAsync(ct),
    cancellationToken);

// Void overload
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

## Provider examples

### SQL Server — transient error filtering

Filter to known SQL Server transient error codes instead of catching all exceptions:

```csharp
// Known SQL Server transient error numbers
private static readonly HashSet<int> SqlTransientErrors = new()
{
    -2,    // Timeout
    20,    // General network error
    64,    // Connection to SQL lost
    233,   // No process at the other end of the pipe
    10053, // Transport-level error
    10054, // Remote host forcibly closed
    10060, // Connection attempt failed
    40197, // Service encountered an error processing your request
    40501, // Service is currently busy
    40613, // Database is not currently available (Azure SQL)
    49918, // Cannot process request — not enough resources
};

services.AddDbContext<AppDbContext>(options =>
    options
        .UseSqlServer(connectionString)
        .AddPollyResilience(pipeline =>
            pipeline.AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(200),
                BackoffType = DelayBackoffType.Exponential,
                ShouldHandle = new PredicateBuilder()
                    .Handle<SqlException>(ex => SqlTransientErrors.Contains(ex.Number))
                    .Handle<TimeoutException>(),
            })));
```

### PostgreSQL (Npgsql)

```csharp
services.AddDbContext<AppDbContext>(options =>
    options
        .UseNpgsql(connectionString)
        .AddPollyResilience(pipeline =>
            pipeline.AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(100),
                BackoffType = DelayBackoffType.Exponential,
                ShouldHandle = new PredicateBuilder()
                    .Handle<NpgsqlException>(ex => ex.IsTransient)
                    .Handle<TimeoutException>(),
            })));
```

### MySQL (Pomelo)

```csharp
services.AddDbContext<AppDbContext>(options =>
    options
        .UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
        .AddPollyResilience(pipeline =>
            pipeline.AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(200),
                ShouldHandle = new PredicateBuilder()
                    .Handle<MySqlException>(ex => ex.IsTransient)
                    .Handle<TimeoutException>(),
            })));
```

---

## ASP.NET Core example with circuit-breaker

```csharp
var builder = WebApplication.CreateBuilder(args);

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

## Full-stack CQRS: PollyMediatR + PollyEFCore

Combine with [PollyMediatR](https://www.nuget.org/packages/PollyMediatR) for end-to-end resilience in CQRS/MediatR apps — the MediatR layer retries the whole handler, and the EF Core layer retries individual commands:

```csharp
// MediatR handler layer (outer) — retries the whole handler on failure
services.AddPollyMediatR(pipeline =>
    pipeline.AddRetry(new RetryStrategyOptions
    {
        MaxRetryAttempts = 2,
        ShouldHandle = new PredicateBuilder().Handle<TransientException>(),
    }));

// EF Core layer (inner) — retries individual SQL commands on transient DB errors
services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(cs).AddPollyResilience(pipeline =>
        pipeline.AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            ShouldHandle = new PredicateBuilder().Handle<SqlException>(IsSqlTransient),
        })));
```

---

## ⚠️ Transaction note

The automatic interceptor wraps individual ADO.NET commands. When using **explicit `DbTransaction`** objects, configure your pipeline to handle only connection-level failures, or use `ExecuteWithResilienceAsync` for full unit-of-work retry control.

---

## Related packages

| Package | Downloads | Description |
|---|---|---|
| [PollyHealthChecks](https://www.nuget.org/packages/PollyHealthChecks) | [![Downloads](https://img.shields.io/nuget/dt/PollyHealthChecks.svg)](https://www.nuget.org/packages/PollyHealthChecks) | ASP.NET Core health checks for Polly v8 circuit breakers |
| [PollyOpenAI](https://www.nuget.org/packages/PollyOpenAI) | [![Downloads](https://img.shields.io/nuget/dt/PollyOpenAI.svg)](https://www.nuget.org/packages/PollyOpenAI) | Polly v8 resilience for OpenAI and Azure OpenAI — retry on 429, Retry-After, circuit breaker |
| [PollyRedis](https://www.nuget.org/packages/PollyRedis) | [![Downloads](https://img.shields.io/nuget/dt/PollyRedis.svg)](https://www.nuget.org/packages/PollyRedis) | Polly v8 resilience for StackExchange.Redis — retry, circuit breaker, timeout |
| [PollySignalR](https://www.nuget.org/packages/PollySignalR) | [![Downloads](https://img.shields.io/nuget/dt/PollySignalR.svg)](https://www.nuget.org/packages/PollySignalR) | Polly v8 exponential back-off reconnect policy for SignalR HubConnection |
| [PollyGrpc](https://www.nuget.org/packages/PollyGrpc) | Polly v8 resilience (retry, CB, timeout) for gRPC .NET clients via Interceptor |
| [PollyKafka](https://www.nuget.org/packages/PollyKafka) | Polly v8 resilience (retry, CB, timeout) for Confluent.Kafka producers and consumers |
| [PollyAzureServiceBus](https://www.nuget.org/packages/PollyAzureServiceBus) | Polly v8 resilience (retry, CB, timeout) for Azure Service Bus senders and receivers |
| [PollyMediatR](https://www.nuget.org/packages/PollyMediatR) | [![Downloads](https://img.shields.io/nuget/dt/PollyMediatR.svg)](https://www.nuget.org/packages/PollyMediatR) | Polly v8 pipelines for MediatR request handlers |
| [PollyBackoff](https://www.nuget.org/packages/PollyBackoff) | [![Downloads](https://img.shields.io/nuget/dt/PollyBackoff.svg)](https://www.nuget.org/packages/PollyBackoff) | Jitter, linear & custom backoff for Polly v8 retry |
| [PollyChaos](https://www.nuget.org/packages/PollyChaos) | [![Downloads](https://img.shields.io/nuget/dt/PollyChaos.svg)](https://www.nuget.org/packages/PollyChaos) | Fault & latency injection (Simmy for Polly v8) |
| [PollyCaching](https://www.nuget.org/packages/PollyCaching) | [![Downloads](https://img.shields.io/nuget/dt/PollyCaching.svg)](https://www.nuget.org/packages/PollyCaching) | Cache-aside resilience strategy for Polly v8 |
| [PollyBulkhead](https://www.nuget.org/packages/PollyBulkhead) | [![Downloads](https://img.shields.io/nuget/dt/PollyBulkhead.svg)](https://www.nuget.org/packages/PollyBulkhead) | Bulkhead / concurrency limiter for Polly v8 |
| [PollyOpenTelemetry](https://www.nuget.org/packages/PollyOpenTelemetry) | [![Downloads](https://img.shields.io/nuget/dt/PollyOpenTelemetry.svg)](https://www.nuget.org/packages/PollyOpenTelemetry) | OpenTelemetry metrics & tracing for Polly v8 |

---

## License

MIT
