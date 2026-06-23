// <copyright file="DatabaseFacadeExtensions.cs" company="Justin Bannister">
// Copyright (c) Justin Bannister. All rights reserved.
// </copyright>

namespace PollyEFCore;

/// <summary>
/// Extension methods on <see cref="DatabaseFacade"/> for explicitly wrapping database
/// operations in a Polly v8 <see cref="ResiliencePipeline"/>.
/// </summary>
/// <remarks>
/// Use these methods when you need fine-grained control over which operations are wrapped
/// — for example, when working inside an explicit transaction or when different queries
/// require different resilience configurations.
/// </remarks>
public static class DatabaseFacadeExtensions
{
    /// <summary>
    /// Executes an asynchronous database operation inside a Polly v8
    /// <see cref="ResiliencePipeline"/>, returning its result.
    /// </summary>
    /// <typeparam name="TResult">The type of value produced by the operation.</typeparam>
    /// <param name="database">The <see cref="DatabaseFacade"/> (e.g. <c>context.Database</c>).</param>
    /// <param name="pipeline">The resilience pipeline to execute the operation through.</param>
    /// <param name="operation">The asynchronous database operation to execute.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task containing the result of the operation.</returns>
    public static Task<TResult> ExecuteWithResilienceAsync<TResult>(
        this DatabaseFacade database,
        ResiliencePipeline pipeline,
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(operation);

        return pipeline
            .ExecuteAsync(ct => new ValueTask<TResult>(operation(ct)), cancellationToken)
            .AsTask();
    }

    /// <summary>
    /// Executes an asynchronous database operation (with no return value) inside a Polly v8
    /// <see cref="ResiliencePipeline"/>.
    /// </summary>
    /// <param name="database">The <see cref="DatabaseFacade"/> (e.g. <c>context.Database</c>).</param>
    /// <param name="pipeline">The resilience pipeline to execute the operation through.</param>
    /// <param name="operation">The asynchronous database operation to execute.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the operation finishes.</returns>
    public static Task ExecuteWithResilienceAsync(
        this DatabaseFacade database,
        ResiliencePipeline pipeline,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(operation);

        return pipeline
            .ExecuteAsync(ct => new ValueTask(operation(ct)), cancellationToken)
            .AsTask();
    }
}
