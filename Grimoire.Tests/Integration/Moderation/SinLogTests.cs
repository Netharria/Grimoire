// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Moderation.SinAdmin.Commands;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using Grimoire.Tests.Integration.FakeDiscord;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Tests.Integration.Moderation;

/// <summary>
///     <c>/SinLog</c>, cases SIN-01 to SIN-15 in <c>docs/testing/03-sins.md</c>. Sins, reasons and pardons are seeded
///     directly. SIN-15 (two guilds) is in A-35.
/// </summary>
[Collection("Test collection")]
public sealed class SinLogTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private readonly GrimoireCoreFactory _database = database;
    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;
    private GuildId GuildId => new(this.Guild.Id);

    public async ValueTask InitializeAsync()
    {
        await this._database.ResetDatabase();
        this._bot = await GrimoireTestBot.StartAsync(this._database);
        await this._bot.ConfigureModerationAsync();
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    /// <summary>SIN-01 and SIN-09: every sin is listed with its id, type, newest reason and moderator.</summary>
    [Fact]
    public async Task All_ListsEverySin_WithItsNewestReasonAndModerator()
    {
        var warn = await this.SeedSinAsync(SinType.Warn, this.Guild.User1, this.Guild.Moderator, "first reason", "newer reason");
        var ban = await this.SeedSinAsync(SinType.Ban, this.Guild.User1, this.Guild.Admin, "spam");

        var reply = await this.SinLogAsync(this.Guild.Moderator, SinQueryType.All, this.Guild.User1);

        reply.IsEphemeral.ShouldBeFalse();
        reply.Embed!["author"]!["name"]!.GetValue<string>().ShouldBe("Sin log for user1");
        var log = reply.Description.ShouldNotBeNull();
        log.ShouldContain($"**{warn.Id} : Warn** : <t:{warn.SinOn.ToUnixTimeSeconds()}:f>\n\tReason: newer reason\n\tModerator: <@{this.Guild.Moderator.Id}>\n");
        log.ShouldContain($"**{ban.Id} : Ban** :");
        log.ShouldContain($"\tModerator: <@{this.Guild.Admin.Id}>");
        log.ShouldNotContain("first reason");
        this._bot.Rest.Unhandled.ShouldBeEmpty();
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>SIN-02.</summary>
    [Theory]
    [InlineData(SinQueryType.Warn, "Warn")]
    [InlineData(SinQueryType.Mute, "Mute")]
    [InlineData(SinQueryType.Ban, "Ban")]
    [InlineData(SinQueryType.Kick, "Kick")]
    public async Task ATypeFilter_ListsOnlyThatType(SinQueryType type, string shown)
    {
        foreach (var sinType in new[] { SinType.Warn, SinType.Mute, SinType.Ban, SinType.Kick })
            await this.SeedSinAsync(sinType, this.Guild.User1, this.Guild.Moderator);

        var log = (await this.SinLogAsync(this.Guild.Moderator, type, this.Guild.User1)).Description.ShouldNotBeNull();

        log.Split("\n").Count(line => line.StartsWith("**", StringComparison.Ordinal)).ShouldBe(1);
        log.ShouldContain($" : {shown}**");
    }

    /// <summary>SIN-03.</summary>
    [Fact]
    public async Task WithoutAUser_ShowsYourOwnLog()
    {
        await this.SeedSinAsync(SinType.Warn, this.Guild.Moderator, this.Guild.Admin);

        (await this.SinLogAsync(this.Guild.Moderator, SinQueryType.All)).Embed!["author"]!["name"]!.GetValue<string>()
            .ShouldBe("Sin log for mod");
    }

    /// <summary>SIN-04 and SIN-05: members without Manage Messages see only their own log, privately.</summary>
    [Fact]
    public async Task AMemberWithoutManageMessages_SeesOnlyTheirOwnLog_Privately()
    {
        await this.SeedSinAsync(SinType.Warn, this.Guild.User1, this.Guild.Moderator);

        var own = await this.SinLogAsync(this.Guild.User1, SinQueryType.All);
        own.IsEphemeral.ShouldBeTrue();
        own.Description.ShouldNotBeNull().ShouldContain(" : Warn**");

        var other = await this.SinLogAsync(this.Guild.User1, SinQueryType.All, this.Guild.Admin);
        other.IsEphemeral.ShouldBeTrue();
        other.Description.ShouldBe("You do not have permission to view other users' logs.");
    }

    /// <summary>SIN-06.</summary>
    [Fact]
    public async Task WithNoSins_SaysSo()
        => (await this.SinLogAsync(this.Guild.Moderator, SinQueryType.All, this.Guild.User1)).Description
            .ShouldBe("That user does not have any logs");

    /// <summary>SIN-07: sins older than the auto-pardon window are left out.</summary>
    [Fact]
    public async Task SinsOlderThanTheAutoPardonWindow_AreLeftOut()
    {
        (await this._bot.Services.GetRequiredService<SettingsModule>()
            .SetAutoPardonDuration(this.GuildId, new ModeratorId(this.Guild.Owner.Id), TimeSpan.FromDays(30), TestContext.Current.CancellationToken)).ShouldSucceed();
        var recent = await this.SeedSinAsync(SinType.Warn, this.Guild.User1, this.Guild.Moderator);
        var old = await this.SeedSinAsync(SinType.Warn, this.Guild.User1, this.Guild.Moderator, at: DateTimeOffset.UtcNow.AddDays(-40));

        var log = (await this.SinLogAsync(this.Guild.Moderator, SinQueryType.All, this.Guild.User1)).Description.ShouldNotBeNull();

        log.ShouldContain($"**{recent.Id} : Warn**");
        log.ShouldNotContain($"**{old.Id} : Warn**");
    }

    /// <summary>SIN-08: a pardoned sin is struck through and says who pardoned it.</summary>
    [Fact]
    public async Task APardonedSin_IsStruckThrough_AndSaysWhoPardonedIt()
    {
        var sin = await this.SeedSinAsync(SinType.Warn, this.Guild.User1, this.Guild.Moderator);
        var pardonedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        await using (var seed = this._database.CreateDbContext())
        {
            seed.Pardons.Add(new Pardon
            {
                SinId = sin.Id,
                GuildId = this.GuildId,
                ModeratorId = new ModeratorId(this.Guild.Admin.Id),
                Reason = ModerationReason.Create("forgiven").ShouldSucceed(),
                SetAt = pardonedAt
            });
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var log = (await this.SinLogAsync(this.Guild.Moderator, SinQueryType.All, this.Guild.User1)).Description.ShouldNotBeNull();

        log.ShouldStartWith($"~~**{sin.Id} : Warn**");
        log.ShouldContain($"~~**Pardoned by: <@{this.Guild.Admin.Id}> on <t:{pardonedAt.ToUnixTimeSeconds()}:f>**");
    }

    /// <summary>SIN-10: a sin without a reason shows a blank reason.</summary>
    [Fact]
    public async Task ASinWithoutAReason_ShowsItBlank()
    {
        await this.SeedSinAsync(SinType.Ban, this.Guild.User1, this.Guild.Moderator);

        (await this.SinLogAsync(this.Guild.Moderator, SinQueryType.All, this.Guild.User1)).Description.ShouldNotBeNull()
            .ShouldContain($"\tReason: \n\tModerator: <@{this.Guild.Moderator.Id}>\n");
    }

    /// <summary>SIN-10: a sin with no known moderator shows System.</summary>
    [Fact(Skip = "Fails: a sin with a System actor can't be saved; EF throws \"Nullable object must have a value\". KI-31")]
    public async Task ASinWithNoKnownModerator_ShowsSystem()
    {
        await this.SeedSinAsync(SinType.Ban, this.Guild.User1, moderator: null);

        (await this.SinLogAsync(this.Guild.Moderator, SinQueryType.All, this.Guild.User1)).Description.ShouldNotBeNull()
            .ShouldContain("\tModerator: System\n");
    }

    /// <summary>SIN-11: the invoker's own counts. Kicks are not counted.</summary>
    [Fact]
    public async Task Mod_CountsTheModeratorsBansMutesAndWarns()
    {
        await this.SeedSinAsync(SinType.Ban, this.Guild.User1, this.Guild.Moderator);
        await this.SeedSinAsync(SinType.Warn, this.Guild.User1, this.Guild.Moderator);
        await this.SeedSinAsync(SinType.Warn, this.Guild.Admin, this.Guild.Moderator);
        await this.SeedSinAsync(SinType.Kick, this.Guild.User1, this.Guild.Moderator);
        await this.SeedSinAsync(SinType.Mute, this.Guild.User1, this.Guild.Admin);

        var embed = (await this.SinLogAsync(this.Guild.Moderator, SinQueryType.Mod)).Embed!;

        embed["author"]!["name"]!.GetValue<string>().ShouldBe("Moderation log for mod");
        Fields(embed).ShouldBe([("Bans", "1"), ("Mutes", "0"), ("Warns", "2")]);
    }

    /// <summary>SIN-12.</summary>
    [Fact(Skip = "Fails: type Mod always counts the invoker's actions, whatever user is given. KI-29")]
    public async Task Mod_ForAnotherModerator_CountsTheirActions()
    {
        await this.SeedSinAsync(SinType.Ban, this.Guild.User1, this.Guild.Admin);

        Fields((await this.SinLogAsync(this.Guild.Moderator, SinQueryType.Mod, this.Guild.Admin)).Embed!)
            .ShouldContain(("Bans", "1"));
    }

    /// <summary>SIN-13: a log long enough to need several pages should show them all.</summary>
    [Fact(Skip = "Fails: each page edits the same response, so only the last page is ever seen. KI-30")]
    public async Task ALongLog_ShowsEveryPage()
    {
        var reason = new string('x', 300);
        var first = await this.SeedSinAsync(SinType.Warn, this.Guild.User1, this.Guild.Moderator, reason, at: DateTimeOffset.UtcNow.AddDays(-1));
        for (var i = 0; i < 9; i++)
            await this.SeedSinAsync(SinType.Warn, this.Guild.User1, this.Guild.Moderator, reason);

        var interaction = this._bot.RunSlashCommand("sin_log", this.Guild.Moderator,
            SlashOption.Enum("type", SinQueryType.All), SlashOption.User("user", this.Guild.User1));
        await this._bot.Rest.WaitForRequestAsync(interaction.IsOriginalResponseEdit);
        await Task.Delay(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        var visible = this._bot.Rest.Requests.Where(request => request.Path.Contains(interaction.Token, StringComparison.Ordinal))
            .Where(request => request.Method == HttpMethod.Post || request.Method == HttpMethod.Patch)
            .Select(request => request.Json?["embeds"]?[0]?["description"]?.ToString() ?? "")
            .ToList();
        // Every page must still be visible, so the first sin's page can't have been overwritten by a later edit.
        visible.Count(page => page.Contains($"**{first.Id} : Warn**", StringComparison.Ordinal)).ShouldBe(1);
        this._bot.Rest.Requests.Count(request => request.Method == HttpMethod.Post && request.Path.EndsWith($"/{interaction.Token}", StringComparison.Ordinal))
            .ShouldBeGreaterThan(0, "later pages should be follow-ups, not edits");
    }

    /// <summary>SIN-14.</summary>
    [Fact]
    public async Task WithModerationDisabled_IsRefused()
    {
        await this._bot.SetModuleAsync(Module.Moderation, false);

        (await this.SinLogAsync(this.Guild.Moderator, SinQueryType.All)).Description.ShouldBe("This module is disabled in this server.");
    }

    private Task<FakeReply> SinLogAsync(FakeUser invoker, SinQueryType type, FakeUser? user = null)
        => this._bot.RunSlashCommandAndWaitAsync("sin_log", invoker,
        [
            SlashOption.Enum("type", type),
            .. user is null ? Array.Empty<SlashOption>() : [SlashOption.User("user", user)]
        ]);

    /// <summary>Seeds a sin, with each reason added a minute apart so the last one is the newest.</summary>
    private async Task<Sin> SeedSinAsync(SinType type, FakeUser user, FakeUser? moderator, string? reason = null,
        string? newerReason = null, DateTimeOffset? at = null)
    {
        ModerationActor actor = moderator is null
            ? new ModerationActor.System()
            : new ModerationActor.Moderator(new ModeratorId(moderator.Id));
        var sinOn = at ?? DateTimeOffset.UtcNow.AddMinutes(-30);
        var userId = new UserId(user.Id);
        var sin = (type switch
        {
            SinType.Warn => Sin.ForWarn(actor, userId, this.GuildId, sinOn),
            SinType.Mute => Sin.ForMute(actor, userId, this.GuildId, sinOn),
            SinType.Ban => Sin.ForBan(actor, userId, this.GuildId, sinOn),
            _ => Sin.ForKick(actor, userId, this.GuildId, sinOn)
        }).ShouldSucceed();
        sin = sin with
        {
            ReasonHistory =
            [
                .. new[] { reason, newerReason }.OfType<string>().Select((text, index) => new SinReasonHistory
                {
                    SinId = default, Reason = ModerationReason.Create(text).ShouldSucceed(), Actor = actor,
                    SetAt = sinOn.AddMinutes(index)
                })
            ]
        };
        await using var seed = this._database.CreateDbContext();
        seed.Sins.Add(sin);
        await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        return sin;
    }

    private static (string, string)[] Fields(System.Text.Json.Nodes.JsonNode embed)
        => [.. embed["fields"]!.AsArray().Select(field => (field!["name"]!.GetValue<string>(), field["value"]!.GetValue<string>()))];
}
