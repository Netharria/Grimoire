// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text.Json.Nodes;
using Grimoire.Features.Shared.GuildIssues;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using Grimoire.Tests.Integration.FakeDiscord;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Tests.Integration.Moderation;

/// <summary><c>/Publish</c> Ban, Unban and Kick, cases SIN-70 to SIN-80 in <c>docs/testing/03-sins.md</c>.</summary>
[Collection("Test collection")]
public sealed class PublishTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private readonly GrimoireCoreFactory _database = database;
    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;
    private SettingsModule Settings => this._bot.Services.GetRequiredService<SettingsModule>();
    private GuildId GuildId => new(this.Guild.Id);
    private FakeChannel BanLog => this.Guild.General;

    public async ValueTask InitializeAsync()
    {
        await this._database.ResetDatabase();
        this._bot = await GrimoireTestBot.StartAsync(this._database);
        await this.ConfigureAsync(this.BanLog);
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    private async Task ConfigureAsync(FakeChannel? banLog)
    {
        await this._bot.ConfigureModerationAsync();
        (await this.Settings.SetLogChannelSetting(GuildLogType.PublicModeration, this.GuildId, new ModeratorId(this.Guild.Owner.Id),
            banLog is null ? null : new ChannelId(banLog.Id))).ShouldBeSetOrUnchanged();
    }

    /// <summary>SIN-70.</summary>
    [Fact]
    public async Task PublishBan_PostsToTheBanLog_RecordsIt_AndLogs()
    {
        var sin = await this.SeedSinAsync(SinType.Ban, "spam");

        (await this.PublishAsync("ban", sin.Id)).Description.ShouldBe($"Successfully published ban : {sin.Id}");

        var post = this._bot.Rest.PostsIn(this.BanLog).ShouldHaveSingleItem();
        var embed = post["embeds"]![0]!;
        embed["title"]!.GetValue<string>().ShouldBe("Ban");
        embed["description"]!.GetValue<string>().ShouldBe(
            $"**Date:** <t:{sin.SinOn.ToUnixTimeSeconds()}:f>\n**User:** user1 ({this.Guild.User1.Id})\n**Reason:** spam");
        await using var dbContext = this._database.CreateDbContext();
        var published = await dbContext.PublishedMessages.SingleAsync(TestContext.Current.CancellationToken);
        published.PublishType.ShouldBe(PublishType.Ban);
        published.MessageId.Value.ToString().ShouldBe(post["id"]!.ToString());
        await this._bot.WaitForModLogEmbedAsync(e => e["description"]?.GetValue<string>()
            == $"{this.Guild.Moderator.MemberMention} published ban reason of sin {sin.Id}");
        this._bot.Rest.Unhandled.ShouldBeEmpty();
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>SIN-71: publishing again edits the same message.</summary>
    [Fact]
    public async Task PublishBan_Again_EditsTheSameMessage()
    {
        var sin = await this.SeedSinAsync(SinType.Ban, "spam");
        await this.PublishAsync("ban", sin.Id);
        await this._bot.RunSlashCommandAndWaitAsync("reason", this.Guild.Moderator,
            SlashOption.String("sin_id", sin.Id.ToString()), SlashOption.String("reason", "spam and scams"));

        await this.PublishAsync("ban", sin.Id);

        this._bot.Rest.PostsIn(this.BanLog).ShouldHaveSingleItem()["embeds"]![0]!["description"]!.GetValue<string>()
            .ShouldEndWith("**Reason:** spam and scams");
        await using var dbContext = this._database.CreateDbContext();
        (await dbContext.PublishedMessages.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(1);
    }

    /// <summary>SIN-72.</summary>
    [Fact(Skip = "Fails: when the published message was deleted, a new one is posted but its id is never stored, so " +
                 "every later publish posts another message. KI-36")]
    public async Task PublishBan_AfterThePostWasDeleted_PostsOnceAndKeepsEditingIt()
    {
        var sin = await this.SeedSinAsync(SinType.Ban, "spam");
        await this.PublishAsync("ban", sin.Id);
        this._bot.Rest.DeleteMessageByHand(this.BanLog, ulong.Parse(this._bot.Rest.PostsIn(this.BanLog).Single()["id"]!.ToString()));

        await this.PublishAsync("ban", sin.Id);
        await this.PublishAsync("ban", sin.Id);

        this._bot.Rest.PostsIn(this.BanLog).Count.ShouldBe(1);
    }

    /// <summary>SIN-73.</summary>
    [Fact]
    public async Task PublishBan_OfASinThatIsNotABan_IsNotFound()
    {
        var sin = await this.SeedSinAsync(SinType.Warn, "x");

        (await this.PublishAsync("ban", sin.Id)).Description.ShouldBe("Could not find a ban with that Sin Id.");
    }

    /// <summary>SIN-74: with no ban log configured, the reply blames channel access.</summary>
    [Fact]
    public async Task PublishBan_WithNoBanLog_SaysItFailed()
    {
        await this.ConfigureAsync(banLog: null);
        var sin = await this.SeedSinAsync(SinType.Ban, "spam");

        (await this.PublishAsync("ban", sin.Id)).Description.ShouldBe(
            $"Failed to publish ban reason. Verify {this.Guild.Bot.MemberMention} has access to send messages in the public ban log channel.");
    }

    /// <summary>SIN-75: a ban log the bot can't send in is refused by Discord, and the error handler explains it.</summary>
    [Fact]
    public async Task PublishBan_ToAChannelTheBotCantSendIn_ShowsTheMissingPermissionsHelp()
    {
        await this._bot.DisposeAsync();
        this._bot = await GrimoireTestBot.StartAsync(this._database, guild => guild.BotPermissions =
            FakeGuild.ViewChannelPermission | FakeGuild.SendMessagesPermission | FakeGuild.EmbedLinksPermission);
        await this.ConfigureAsync(this.Guild.Private);
        var sin = await this.SeedSinAsync(SinType.Ban, "spam");
        var issue = GuildIssueCatalog.BotMissingPermissions(this.GuildId, "Publish Ban");

        (await this.PublishAsync("ban", sin.Id)).Description.ShouldBe($"{issue.Summary}\n**Suggested fix:** {issue.SuggestedFix}");
    }

    /// <summary>SIN-76 and SIN-77: Unban needs a pardon, and then publishes its reason and date.</summary>
    [Fact]
    public async Task PublishUnban_NeedsAPardon_ThenPublishesIt()
    {
        var sin = await this.SeedSinAsync(SinType.Ban, "spam");

        (await this.PublishAsync("unban", sin.Id)).Description
            .ShouldBe("The ban must be pardoned first before the unban can be published.");

        await this._bot.RunSlashCommandAndWaitAsync("pardon", this.Guild.Moderator,
            SlashOption.String("sin_id", sin.Id.ToString()), SlashOption.String("reason", "appealed"));
        await using var dbContext = this._database.CreateDbContext();
        var pardon = await dbContext.Pardons.SingleAsync(TestContext.Current.CancellationToken);

        (await this.PublishAsync("unban", sin.Id)).Description.ShouldBe($"Successfully published unban : {sin.Id}");
        var embed = this._bot.Rest.PostsIn(this.BanLog).ShouldHaveSingleItem()["embeds"]![0]!;
        embed["title"]!.GetValue<string>().ShouldBe("Unban");
        embed["description"]!.GetValue<string>().ShouldBe(
            $"**Date:** <t:{pardon.SetAt.ToUnixTimeSeconds()}:f>\n**User:** user1 ({this.Guild.User1.Id})\n**Reason:** appealed");
    }

    /// <summary>SIN-78.</summary>
    [Fact]
    public async Task PublishKick_PostsAKickEmbed()
    {
        var sin = await this.SeedSinAsync(SinType.Kick, "rude");

        (await this.PublishAsync("kick", sin.Id)).Description.ShouldBe($"Successfully published kick : {sin.Id}");

        this._bot.Rest.PostsIn(this.BanLog).ShouldHaveSingleItem()["embeds"]![0]!["title"]!.GetValue<string>().ShouldBe("Kick");
        await using var dbContext = this._database.CreateDbContext();
        (await dbContext.PublishedMessages.SingleAsync(TestContext.Current.CancellationToken)).PublishType.ShouldBe(PublishType.Kick);
    }

    /// <summary>SIN-79.</summary>
    [Fact(Skip = "Fails: the username lookup uses First() on an empty history, so the command errors. KI-33")]
    public async Task PublishBan_ForAUserWithNoUsernameHistory_StillPublishes()
    {
        var sin = await this.SeedSinAsync(SinType.Ban, "spam", new FakeUser(1_100_000_000_000_000_094, "stranger"), withUsername: false);

        (await this.PublishAsync("ban", sin.Id)).Description.ShouldBe($"Successfully published ban : {sin.Id}");
    }

    /// <summary>SIN-80.</summary>
    [Theory]
    [InlineData("permission")]
    [InlineData("module")]
    public async Task Publish_NeedsManageMessagesAndTheModule(string missing)
    {
        var sin = await this.SeedSinAsync(SinType.Ban, "spam");
        if (missing == "module")
            await this._bot.SetModuleAsync(Module.Moderation, false);

        var reply = await this._bot.RunSlashCommandAndWaitAsync("publish", missing == "module" ? this.Guild.Moderator : this.Guild.User1,
            SlashOption.Subcommand("ban", SlashOption.String("sin_id", sin.Id.ToString())));

        reply.Description.ShouldBe(missing == "module"
            ? "This module is disabled in this server."
            : "You do not have the required permissions to use this command.");
        this._bot.Rest.PostsIn(this.BanLog).ShouldBeEmpty();
    }

    private Task<FakeReply> PublishAsync(string type, SinId sinId)
        => this._bot.RunSlashCommandAndWaitAsync("publish", this.Guild.Moderator,
            SlashOption.Subcommand(type, SlashOption.String("sin_id", sinId.ToString())));

    private async Task<Sin> SeedSinAsync(SinType type, string reason, FakeUser? user = null, bool withUsername = true)
    {
        var target = user ?? this.Guild.User1;
        var actor = new ModerationActor.Moderator(new ModeratorId(this.Guild.Admin.Id));
        var at = DateTimeOffset.UtcNow.AddHours(-1);
        var userId = new UserId(target.Id);
        var sin = (type switch
        {
            SinType.Ban => Sin.ForBan(actor, userId, this.GuildId, at),
            SinType.Kick => Sin.ForKick(actor, userId, this.GuildId, at),
            _ => Sin.ForWarn(actor, userId, this.GuildId, at)
        }).ShouldSucceed() with
        {
            ReasonHistory = [new SinReasonHistory { SinId = default, Reason = ModerationReason.Create(reason).ShouldSucceed(), Actor = actor, SetAt = at }]
        };
        await using var seed = this._database.CreateDbContext();
        seed.Sins.Add(sin);
        if (withUsername)
            seed.UsernameHistory.Add(new UsernameHistory
            {
                UserId = userId,
                Username = Username.Create(target.Username).ShouldSucceed(),
                Timestamp = DateTimeOffset.UtcNow.AddMinutes(1)
            });
        await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        return sin;
    }
}
