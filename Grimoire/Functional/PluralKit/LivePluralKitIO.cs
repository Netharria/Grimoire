// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Threading.RateLimiting;
using Grimoire.Features.Shared.PluralKit;
using Grimoire.Features.Shared.PluralKit.Models;
using Grimoire.Functional.Capabilities;
using LanguageExt;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Grimoire.Functional.PluralKit;

/// <summary>
///     PluralKit's message lookup over the named <see cref="HttpClient" /> (spec 1.5). A singleton that owns the one
///     rate limiter, so every lookup in the process shares its bucket. No try/catch outside <see cref="Attempt" />.
/// </summary>
internal sealed class LivePluralKitIO(IHttpClientFactory httpClientFactory) : IPluralKitIO, IDisposable
{
    public const string ClientName = "Pluralkit";
    private const string MessageEndpoint = "messages/";

    /// <summary>
    ///     5 a second, half of PluralKit's published 10 for reads, since the docs don't say what the limit is per (Q24,
    ///     spec 1.5 OQ-1). One token every 200 ms with a burst of 2, so no second ever sees more than 7: a bucket of 5
    ///     refilled 5 at a time would let 10 through when the refill lands mid-second. A queue of 50 keeps the longest
    ///     wait at about 10 s.
    /// </summary>
    internal static TokenBucketRateLimiterOptions Limits => new()
    {
        TokenLimit = 2,
        TokensPerPeriod = 1,
        ReplenishmentPeriod = TimeSpan.FromMilliseconds(200),
        QueueLimit = 50,
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        AutoReplenishment = true
    };

    private readonly TokenBucketRateLimiter _limiter = new(Limits);

    /// <summary>The bucket's queue and tokens, for section 1.6's "PluralKit queue" gauge.</summary>
    public RateLimiterStatistics? Statistics => this._limiter.GetStatistics();

    public void Dispose() => this._limiter.Dispose();

    public bool IsEnabled => true;

    public Schedule ReadRetry => PluralKitRetry.Reads;

    public TimeSpan Cap => PluralKitRetry.Cap;

    public IO<Option<PluralKitMessage>> GetMessage(MessageId message) =>
        this.Attempt(message).Bind(result => result.Match(Succ: IO.pure, Fail: IO.fail<Option<PluralKitMessage>>));

    private IO<Fin<Option<PluralKitMessage>>> Attempt(MessageId message) =>
        IO.liftAsync(async env =>
        {
            try
            {
                using var lease = await this._limiter.AcquireAsync(1, env.Token);
                if (!lease.IsAcquired)
                    return Fin.Fail<Option<PluralKitMessage>>(PluralKitErrors.QueueFull());

                using var response = await httpClientFactory.CreateClient(ClientName)
                    .GetAsync(MessageEndpoint + message.Value, env.Token);
                var body = await response.Content.ReadAsStringAsync(env.Token);
                return PluralKitErrors.FromResponse(response.StatusCode, body);
            }
            catch (Exception exception)
            {
                return Fin.Fail<Option<PluralKitMessage>>(PluralKitErrors.Classify(exception, env.Token));
            }
        });
}

/// <summary>
///     PluralKit when it isn't configured (KI-101): every lookup is "not asked". Says why once, when it's built, which
///     is once per process since it's a singleton.
/// </summary>
internal sealed partial class DisabledPluralKitIO : IPluralKitIO
{
    public DisabledPluralKitIO(Seq<string> problems, ILogger<DisabledPluralKitIO> logger)
        => LogDisabled(logger, string.Join("; ", problems));

    public bool IsEnabled => false;

    public IO<Option<PluralKitMessage>> GetMessage(MessageId message) =>
        IO.fail<Option<PluralKitMessage>>(Error.New("PluralKit isn't configured; check IsEnabled before asking."));

    public Schedule ReadRetry => Schedule.Never;

    public TimeSpan Cap => TimeSpan.Zero;

    [LoggerMessage(LogLevel.Warning, "PluralKit integration is off: {Problems}.")]
    static partial void LogDisabled(ILogger logger, string problems);
}

/// <summary>Registers PluralKit from its settings, which are read when first needed, not at registration.</summary>
internal static class PluralKitServiceRegistration
{
    extension(IServiceCollection services)
    {
        /// <summary>
        ///     The settings are read from the built configuration, so a source added after <c>AddGrimoire</c> (as the
        ///     tests do) still counts. With a key missing, PluralKit is off and the client is never used, so its setup
        ///     can't throw (KI-101).
        /// </summary>
        public IServiceCollection AddPluralKit()
        {
            services.AddSingleton(provider =>
                new PluralKitConfiguration(PluralKitOptions.From(provider.GetRequiredService<IConfiguration>())));
            services.AddSingleton<IPluralKitIO>(provider =>
                provider.GetRequiredService<PluralKitConfiguration>().Options.Match<IPluralKitIO>(
                    Right: _ => ActivatorUtilities.CreateInstance<LivePluralKitIO>(provider),
                    Left: problems => new DisabledPluralKitIO(problems,
                        provider.GetRequiredService<ILogger<DisabledPluralKitIO>>())));
            services.AddHttpClient(LivePluralKitIO.ClientName)
                .ConfigureHttpClient((provider, client) =>
                    provider.GetRequiredService<PluralKitConfiguration>().Options.IfRight(options =>
                    {
                        client.BaseAddress = options.Endpoint;
                        client.Timeout = PluralKitRetry.AttemptTimeout;
                        // Without validation: a user agent the parser dislikes would otherwise throw here.
                        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", options.UserAgent);
                        client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", options.Token);
                    }));
            return services;
        }
    }
}

/// <summary>The parsed settings, or what's missing. One per process.</summary>
internal sealed record PluralKitConfiguration(Either<Seq<string>, PluralKitOptions> Options);
