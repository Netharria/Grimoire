// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Net;
using Grimoire.Features.Shared.PluralKit.Models;
using Grimoire.Functional.Capabilities;
using Grimoire.Functional.PluralKit;
using Grimoire.IntegrationTests.FakeDiscord;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.IntegrationTests.Shared.PluralKit;

/// <summary>
///     Spec 1.5: the live capability, its HttpClient and its rate limiter, against <see cref="FakePluralKit" />. No
///     database or bot: only the registration <c>AddGrimoire</c> uses.
/// </summary>
public sealed class LivePluralKitTests : IAsyncDisposable
{
    private static readonly FakeUser Sender = new(1_100_000_000_000_007_001, "pk");
    private static readonly FakeGuild Guild = new();

    private readonly FakePluralKit _pluralKit = new();
    private readonly ServiceProvider _services;

    public LivePluralKitTests()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["pluralkitApiEndpoint"] = "http://pluralkit.fake.invalid/v2",
            ["pluralkitUserAgent"] = "Grimoire.Tests",
            ["pluralkitToken"] = "fake-token"
        }).Build();
        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IConfiguration>(configuration)
            .AddPluralKit();
        services.AddHttpClient(LivePluralKitIO.ClientName).ConfigurePrimaryHttpMessageHandler(() => this._pluralKit);
        this._services = services.BuildServiceProvider();
    }

    private IPluralKitIO PluralKit => this._services.GetRequiredService<IPluralKitIO>();

    public async ValueTask DisposeAsync() => await this._services.DisposeAsync();

    private async Task<Fin<Option<PluralKitMessage>>> GetAsync(ulong id)
    {
        using var envIO = EnvIO.New(token: TestContext.Current.CancellationToken);
        return await this.PluralKit.GetMessage(new MessageId(id)).Map(Fin.Succ).Catch(error => Fin.Fail<Option<PluralKitMessage>>(error))
            .As().RunAsync(envIO);
    }

    private static PluralKitProblem ProblemOf(Error error)
        => PluralKitErrors.ProblemOf(error).Match(problem => problem, () => throw new ShouldAssertException($"no problem in {error}"));

    [Fact]
    public async Task AKnownMessage_IsRead_ByEitherId_UnderTheV2Path()
    {
        this._pluralKit.AddProxy(100, 200, Sender, Guild, "abcde", "fghij");

        foreach (var id in new ulong[] { 100, 200 })
        {
            var message = (await this.GetAsync(id)).ShouldSucceed().Match(found => found, () => throw new ShouldAssertException("None"));
            message.Id.ShouldBe("200");
            message.OriginalId.ShouldBe("100");
            message.PluralKitSystem!.Id.ShouldBe("abcde");
        }

        this._pluralKit.Lookups.ShouldBe(["v2/messages/100", "v2/messages/200"]);
    }

    [Fact]
    public async Task TheUserAgentAndToken_AreSent()
    {
        await this.GetAsync(1);

        this._pluralKit.LastHeaders!.GetValues("User-Agent").ShouldBe(["Grimoire.Tests"]);
        this._pluralKit.LastHeaders.GetValues("Authorization").ShouldBe(["fake-token"]);
    }

    [Fact]
    public async Task AnUnknownMessage_IsNone()
        => (await this.GetAsync(1)).ShouldSucceed().IsNone.ShouldBeTrue();

    [Fact]
    public async Task ABare404_IsARefusal_NamingTheEndpoint()
    {
        this._pluralKit.FailWith(HttpStatusCode.NotFound);

        var error = (await this.GetAsync(1)).ShouldFail();

        ProblemOf(error).ShouldBe(PluralKitProblem.Refused);
        PluralKitErrors.ExceptionOf(error).Match(e => e.Message, () => "").ShouldContain("pluralkitApiEndpoint");
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, PluralKitProblem.Outage)]
    [InlineData(HttpStatusCode.Unauthorized, PluralKitProblem.Refused)]
    [InlineData(HttpStatusCode.Forbidden, PluralKitProblem.Refused)]
    public async Task AFailingStatus_IsItsProblem(HttpStatusCode status, PluralKitProblem problem)
    {
        this._pluralKit.FailWith(status, """{"message": "nope", "code": 0}""");

        ProblemOf((await this.GetAsync(1)).ShouldFail()).ShouldBe(problem);
    }

    [Fact]
    public async Task A429_IsRateLimited_WithItsRetryAfter()
    {
        this._pluralKit.RateLimitWith(19);

        var error = (await this.GetAsync(1)).ShouldFail();

        ProblemOf(error).ShouldBe(PluralKitProblem.RateLimited);
        PluralKitErrors.IsTransient(error).ShouldBeTrue();
        PluralKitErrors.ExceptionOf(error).Match(e => e.Message, () => "").ShouldContain("19 ms");
    }

    [Fact]
    public async Task ABodyThatIsntJson_IsUnreadable()
    {
        this._pluralKit.FailWith(HttpStatusCode.OK, "<html>Service unavailable</html>");

        ProblemOf((await this.GetAsync(1)).ShouldFail()).ShouldBe(PluralKitProblem.Unreadable);
    }

    [Fact]
    public async Task ASlowAnswer_TimesOut_AsATransientOutage()
    {
        this._pluralKit.Delay = PluralKitRetry.AttemptTimeout + TimeSpan.FromSeconds(1);

        var error = (await this.GetAsync(1)).ShouldFail();

        ProblemOf(error).ShouldBe(PluralKitProblem.Outage);
        PluralKitErrors.IsTransient(error).ShouldBeTrue();
    }

    [Fact]
    public async Task TheBucket_KeepsUnderTheLimit_AndAnswersEveryLookup()
    {
        var results = await Task.WhenAll(Enumerable.Range(1, 20).Select(id => this.GetAsync((ulong)id)));

        results.ShouldAllBe(result => result.IsSucc);
        // The burst plus a second's refills: 7, under PluralKit's published 10.
        var limits = LivePluralKitIO.Limits;
        var mostInASecond = limits.TokenLimit
                            + limits.TokensPerPeriod * (int)(TimeSpan.FromSeconds(1) / limits.ReplenishmentPeriod);
        mostInASecond.ShouldBeLessThan(10);
        this._pluralKit.MostInOneSecond.ShouldBeLessThanOrEqualTo(mostInASecond);
    }

    [Fact]
    public async Task AFullQueue_RefusesAtOnce()
    {
        // A few more than the burst and the queue hold, so a token refilled while they start can't make room.
        var limits = LivePluralKitIO.Limits;
        var lookups = Enumerable.Range(1, limits.TokenLimit + limits.QueueLimit + 5)
            .Select(id => this.GetAsync((ulong)id)).ToList();

        var started = DateTimeOffset.UtcNow;
        var overflow = await this.GetAsync(999);

        ProblemOf(overflow.ShouldFail()).ShouldBe(PluralKitProblem.QueueFull);
        (DateTimeOffset.UtcNow - started).ShouldBeLessThan(TimeSpan.FromSeconds(1));
        var results = await Task.WhenAll(lookups);
        results.Count(result => result.IsSucc).ShouldBeGreaterThanOrEqualTo(limits.TokenLimit + limits.QueueLimit);
        results.Where(result => result.IsFail).Select(result => ProblemOf(result.ShouldFail()))
            .ShouldAllBe(problem => problem == PluralKitProblem.QueueFull);
    }
}
