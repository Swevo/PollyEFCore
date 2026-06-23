// <copyright file="ResilienceDbCommandInterceptor.cs" company="Justin Bannister">
// Copyright (c) Justin Bannister. All rights reserved.
// </copyright>

namespace PollyEFCore;

/// <summary>
/// An EF Core <see cref="DbCommandInterceptor"/> that executes every database command
/// inside a Polly v8 <see cref="ResiliencePipeline"/>.
/// Register it via
/// <see cref="DbContextOptionsBuilderExtensions.AddPollyResilience(DbContextOptionsBuilder, Action{ResiliencePipelineBuilder})"/>.
/// </summary>
/// <remarks>
/// This interceptor wraps the underlying ADO.NET command execution directly.
/// Avoid using it alongside explicit <see cref="DbTransaction"/> objects unless your
/// resilience pipeline is configured to handle only pre-connection failures.
/// </remarks>
public sealed class ResilienceDbCommandInterceptor : DbCommandInterceptor
{
    private readonly ResiliencePipeline _pipeline;

    /// <summary>
    /// Initialises a new instance of <see cref="ResilienceDbCommandInterceptor"/>
    /// with the supplied <paramref name="pipeline"/>.
    /// </summary>
    public ResilienceDbCommandInterceptor(ResiliencePipeline pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        _pipeline = pipeline;
    }

    /// <inheritdoc/>
    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        if (result.HasResult)
            return result;

        var reader = await _pipeline.ExecuteAsync(
            ct => new ValueTask<DbDataReader>(command.ExecuteReaderAsync(ct)),
            cancellationToken);

        return InterceptionResult<DbDataReader>.SuppressWithResult(reader);
    }

    /// <inheritdoc/>
    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (result.HasResult)
            return result;

        var count = await _pipeline.ExecuteAsync(
            ct => new ValueTask<int>(command.ExecuteNonQueryAsync(ct)),
            cancellationToken);

        return InterceptionResult<int>.SuppressWithResult(count);
    }

    /// <inheritdoc/>
    public override async ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        if (result.HasResult)
            return result;

        var value = await _pipeline.ExecuteAsync(
            ct => new ValueTask<object?>(command.ExecuteScalarAsync(ct)),
            cancellationToken);

        return InterceptionResult<object>.SuppressWithResult(value!);
    }
}
