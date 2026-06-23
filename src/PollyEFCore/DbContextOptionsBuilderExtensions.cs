// <copyright file="DbContextOptionsBuilderExtensions.cs" company="Justin Bannister">
// Copyright (c) Justin Bannister. All rights reserved.
// </copyright>

namespace PollyEFCore;

/// <summary>
/// Extension methods for adding Polly v8 resilience to <see cref="DbContextOptionsBuilder"/>.
/// </summary>
public static class DbContextOptionsBuilderExtensions
{
    /// <summary>
    /// Registers a <see cref="ResilienceDbCommandInterceptor"/> that wraps every EF Core
    /// database command in a Polly v8 <see cref="ResiliencePipeline"/> built from
    /// <paramref name="configure"/>.
    /// </summary>
    /// <param name="optionsBuilder">The EF Core options builder.</param>
    /// <param name="configure">
    /// A delegate that configures the <see cref="ResiliencePipelineBuilder"/>
    /// (e.g. adds retry, timeout, circuit-breaker strategies).
    /// </param>
    /// <returns>The same <see cref="DbContextOptionsBuilder"/> for chaining.</returns>
    public static DbContextOptionsBuilder AddPollyResilience(
        this DbContextOptionsBuilder optionsBuilder,
        Action<ResiliencePipelineBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new ResiliencePipelineBuilder();
        configure(builder);
        return optionsBuilder.AddPollyResilience(builder.Build());
    }

    /// <summary>
    /// Registers a <see cref="ResilienceDbCommandInterceptor"/> that wraps every EF Core
    /// database command in the supplied pre-built <paramref name="pipeline"/>.
    /// </summary>
    /// <param name="optionsBuilder">The EF Core options builder.</param>
    /// <param name="pipeline">
    /// A fully configured <see cref="ResiliencePipeline"/> to apply to every database command.
    /// </param>
    /// <returns>The same <see cref="DbContextOptionsBuilder"/> for chaining.</returns>
    public static DbContextOptionsBuilder AddPollyResilience(
        this DbContextOptionsBuilder optionsBuilder,
        ResiliencePipeline pipeline)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        ArgumentNullException.ThrowIfNull(pipeline);

        return optionsBuilder.AddInterceptors(new ResilienceDbCommandInterceptor(pipeline));
    }
}
