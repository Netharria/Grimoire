// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Globalization;
using Grimoire.Features.Moderation.Kick;
using Grimoire.Features.Shared.SharedDtos;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using Grimoire.Tests.Integration.FakeDiscord;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Tests.Integration.Moderation;

/// <summary>
///     Area 02 cross-checks, MOD-190 to MOD-196 in <c>docs/testing/02-moderation-actions.md</c>. MOD-191 is covered by
///     the bot-permission tests in each command's class, MOD-192 (reviewing alerts) stays manual, MOD-195 by the expiry
///     tests, which seed records as a restarted bot finds them, and MOD-196 (two guilds) is in A-35. The bot registers
///     <c>/Kick</c> itself, because production doesn't (KI-25).
/// </summary>
[Collection("Test collection")]
public sealed class ModerationCrossCheckTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private static readonly TimeSpan NothingHappensWindow = TimeSpan.FromSeconds(2);

    private readonly GrimoireCoreFactory _database = database;
    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;
    private SettingsModule Settings => this._bot.Services.GetRequiredService<SettingsModule>();
    private GuildId GuildId => new(this.Guild.Id);

    public async ValueTask InitializeAsync()
    {
        await this._database.ResetDatabase();
        this._bot = await StartBotAsync(this._database);
        await this.ConfigureAsync();
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    private static Task<GrimoireTestBot> StartBotAsync(GrimoireCoreFactory database, Action<FakeGuild>? configureGuild = null)
        => GrimoireTestBot.StartAsync(database, configureGuild, [typeof(KickUser)]);

    private async Task ConfigureAsync()
    {
        await this._bot.ConfigureModerationAsync();
        var muteRole = this._bot.Rest.CreateRole("Muted");
        (await this.Settings.SetMuteRole(this.GuildId, new ModeratorId(this.Guild.Owner.Id), new RoleId(muteRole))).ShouldBeSetOrUnchanged();
    }

    /// <summary>MOD-190: one sin per action, each with its own id and type, and each id shown in its reply.</summary>
    [Fact]
    public async Task EachAction_RecordsItsOwnSin()
    {
        var replies = new[]
        {
            await this._bot.RunSlashCommandAndWaitAsync("warn", this.Guild.Admin, SlashOption.User("user", this.Guild.User1), SlashOption.String("reason", "x")),
            await this.MuteAsync(),
            await this._bot.RunSlashCommandAndWaitAsync("kick", this.Guild.Admin, SlashOption.User("member", this.Guild.User1)),
            await this._bot.RunSlashCommandAndWaitAsync("ban", this.Guild.Admin, SlashOption.User("user", this.Guild.User1))
        };

        await using var dbContext = this._database.CreateDbContext();
        var sins = await dbContext.Sins.OrderBy(sin => sin.SinOn).ToListAsync(TestContext.Current.CancellationToken);
        sins.Select(sin => sin.SinType).ShouldBe([SinType.Warn, SinType.Mute, SinType.Kick, SinType.Ban]);
        sins.Select(sin => sin.Id).Distinct().Count().ShouldBe(4);
        replies.Select(reply => SinIdField(reply)).ShouldBe(sins.Select(sin => $"**{sin.Id.Value.ToString(CultureInfo.InvariantCulture)}**"));
    }

    /// <summary>MOD-193: with the mod log off, every action still works and nothing is posted or logged as an error.</summary>
    [Fact]
    public async Task WithTheModLogOff_ActionsStillWork_AndNothingIsPosted()
    {
        (await this.Settings.SetLogChannelSetting(GuildLogType.Moderation, this.GuildId, new ModeratorId(this.Guild.Owner.Id), null, TestContext.Current.CancellationToken))
            .ShouldBeSetOrUnchanged();

        await this._bot.RunSlashCommandAndWaitAsync("warn", this.Guild.Admin, SlashOption.User("user", this.Guild.User1), SlashOption.String("reason", "x"));
        await this.MuteAsync();
        await this._bot.RunSlashCommandAndWaitAsync("kick", this.Guild.Admin, SlashOption.User("member", this.Guild.User1));
        await this._bot.RunSlashCommandAndWaitAsync("ban", this.Guild.Admin, SlashOption.User("user", this.Guild.User1));
        (await this._bot.RunSlashCommandAndWaitAsync(this.Guild.General, "lock", this.Guild.Admin,
                SlashOption.Enum("duration_type", DurationType.Minutes), SlashOption.Integer("duration_amount", 2)))
            .Description.ShouldBe($"{this.Guild.General.Mention} has been locked for 2 Minutes");

        await using var dbContext = this._database.CreateDbContext();
        (await dbContext.Sins.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(4);
        (await this._bot.Rest.ReceivesRequestWithinAsync(this._bot.IsModLogPost, NothingHappensWindow)).ShouldBeFalse();
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>MOD-194: a mod log the bot can't write to doesn't fail the command; the log reports it instead.</summary>
    [Fact]
    public async Task WithAModLogTheBotCantWriteTo_TheCommandStillSucceeds()
    {
        await this._bot.DisposeAsync();
        this._bot = await StartBotAsync(this._database, guild => guild.BotPermissions =
            FakeGuild.ViewChannelPermission | FakeGuild.SendMessagesPermission | FakeGuild.EmbedLinksPermission | FakeGuild.KickMembersPermission);
        await this.ConfigureAsync();
        (await this.Settings.SetLogChannelSetting(GuildLogType.Moderation, this.GuildId, new ModeratorId(this.Guild.Owner.Id), new ChannelId(this.Guild.Private.Id), TestContext.Current.CancellationToken)).ShouldBeSetOrUnchanged();

        var reply = await this._bot.RunSlashCommandAndWaitAsync("kick", this.Guild.Admin, SlashOption.User("member", this.Guild.User1));

        reply.Embed!["author"]!["name"]!.GetValue<string>().ShouldBe("Kick");
        this._bot.Rest.Kicked.ShouldContain(this.Guild.User1.Id);
        var failure = await GrimoireTestBot.EventuallyAsync(
            () => Task.FromResult(this._bot.Logs.Errors.FirstOrDefault(error => error.Contains("guild log", StringComparison.OrdinalIgnoreCase))),
            error => error is not null);
        failure.ShouldNotBeNull("GuildLogPublisher logs the refused post");
    }

    private Task<FakeReply> MuteAsync()
        => this._bot.RunSlashCommandAndWaitAsync("mute", this.Guild.Admin, SlashOption.User("user", this.Guild.User1),
            SlashOption.Enum("duration_type", DurationType.Minutes), SlashOption.Integer("duration_amount", 5));

    private static string SinIdField(FakeReply reply)
        => reply.Embed!["fields"]!.AsArray().Single(field => field!["name"]!.GetValue<string>() == "Sin Id")!["value"]!.GetValue<string>();
}
