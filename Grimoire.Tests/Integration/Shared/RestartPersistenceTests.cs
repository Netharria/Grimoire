// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text.Json.Nodes;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using Grimoire.Tests.Integration.FakeDiscord;
using Microsoft.Extensions.DependencyInjection;
using ModuleArgument = Grimoire.Features.Shared.Commands.ModuleCommands.ModuleArguments;

namespace Grimoire.Tests.Integration.Shared;

/// <summary>
///     Restart persistence, section G of <c>docs/testing/09-cross-cutting.md</c>. Each test sets something up, stops
///     the bot and starts a new one on the same database. The fake guild is rebuilt on restart, so roles a test made
///     are made again; role ids come from a fixed counter and so match.
/// </summary>
[Collection("Test collection")]
public sealed class RestartPersistenceTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private static readonly TimeSpan NothingHappensWindow = TimeSpan.FromSeconds(2);

    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;
    private SettingsModule Settings => this._bot.Services.GetRequiredService<SettingsModule>();
    private GuildId GuildId => new(this.Guild.Id);
    private ModeratorId Owner => new(this.Guild.Owner.Id);
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await database.ResetDatabase();
        await this.StartAsync();
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    private async Task StartAsync()
    {
        this._bot = await GrimoireTestBot.StartAsync(database);
        await this._bot.ConfigureModerationAsync();
    }

    private async Task RestartAsync()
    {
        await this._bot.DisposeAsync();
        await this.StartAsync();
    }

    /// <summary>XC-111: module states set through /Modules Set are the same after a restart.</summary>
    [Fact]
    public async Task ModuleStates_SurviveARestart()
    {
        await this.SetModuleAsync(ModuleArgument.Leveling, true);
        await this.SetModuleAsync(ModuleArgument.Commands, true);
        await this.SetModuleAsync(ModuleArgument.Moderation, false);
        var before = (await this.ModulesViewAsync()).Description;

        // Restart without ConfigureModerationAsync, which would turn Moderation back on.
        await this._bot.DisposeAsync();
        this._bot = await GrimoireTestBot.StartAsync(database);

        (await this.ModulesViewAsync()).Description.ShouldBe(before);
        before.ShouldBe("**Leveling Enabled:** True\n**User Log Enabled:** False\n**Message Log Enabled:** False\n" +
                        "**Moderation Enabled:** False\n**Commands Enabled:** True\n**Anti Spam Enabled:** False\n");
    }

    /// <summary>XC-115: a thread lock is read from the database after a restart, so a new post is still deleted.</summary>
    [Fact]
    public async Task ThreadLock_SurvivesARestart()
    {
        await this._bot.LockThreadAsync(this.Guild.PublicThread, TimeSpan.FromMinutes(30));

        await this.RestartAsync();
        var messageId = this._bot.SendMessage(this.Guild.User1, this.Guild.PublicThread, "still locked?");

        await this._bot.Rest.WaitForRequestAsync(request => request.Method == HttpMethod.Delete
                                                            && request.Path == $"channels/{this.Guild.PublicThread.Id}/messages/{messageId}");
    }

    /// <summary>XC-116: a member muted before a restart who leaves and rejoins after it gets the mute role back.</summary>
    [Fact]
    public async Task Mute_SurvivesARestart_AndIsGivenBackOnRejoin()
    {
        var muteRoleId = this._bot.Rest.CreateRole("Muted");
        (await this.Settings.SetMuteRole(this.GuildId, this.Owner, new RoleId(muteRoleId), cancellationToken: TestContext.Current.CancellationToken)).ShouldSucceed();
        var now = DateTimeOffset.UtcNow;
        await using (var settingsContext = database.CreateSettingsDbContext())
        {
            settingsContext.Mutes.Add(MuteAdded.Create(new UserId(this.Guild.User1.Id), this.GuildId,
                new ModeratorId(this.Guild.Moderator.Id), new SinId(1), now.AddMinutes(-1), now.AddMinutes(30),
                new RoleId(muteRoleId)).ShouldSucceed());
            await settingsContext.SaveChangesAsync(Token);
        }

        await this.RestartAsync();
        this._bot.Rest.CreateRole("Muted").ShouldBe(muteRoleId);
        await Task.Delay(TimeSpan.FromMilliseconds(200), Token);
        this._bot.MemberLeaves(this.Guild.User1);
        await Task.Delay(TimeSpan.FromMilliseconds(200), Token);
        this._bot.MemberJoins(this.Guild.User1);

        var grant = await this._bot.Rest.WaitForRequestAsync(request => request.Method == HttpMethod.Put
                                                                        && request.Path == $"guilds/{this.Guild.Id}/members/{this.Guild.User1.Id}/roles/{muteRoleId}");
        grant.AuditLogReason.ShouldBe("Rejoined while muted");
    }

    /// <summary>XC-117: the XP timeout is read from <c>XpHistory</c>, so a restart doesn't hand out XP early.</summary>
    [Fact]
    public async Task XpTimeout_SurvivesARestart()
    {
        await this._bot.SetModuleAsync(Module.Leveling, true);
        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "first");
        (await this.EarnedCountAsync(this.Guild.User1, count => count > 0)).ShouldBe(1);

        await this.RestartAsync();
        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "second");

        (await this.EarnedCountAsync(this.Guild.User1, count => count > 1, NothingHappensWindow)).ShouldBe(1);
    }

    /// <summary>
    ///     XC-118: spam points live in memory and start from zero after a restart, but the mute length counts the day's
    ///     mute sins in the database, so the second spam mute is 2 minutes, not 1.
    /// </summary>
    [Fact]
    public async Task SpamMuteLength_CountsMutesFromBeforeARestart()
    {
        await this._bot.SetModuleAsync(Module.AntiSpam, true);
        var muteRoleId = this._bot.Rest.CreateRole("Muted");
        (await this.Settings.SetMuteRole(this.GuildId, this.Owner, new RoleId(muteRoleId), cancellationToken: TestContext.Current.CancellationToken)).ShouldSucceed();
        await this.SpamAsync();
        Field(await this.WaitForSpamMuteAsync(), "Length").ShouldBe("1 minute(s)");
        await Task.Delay(NothingHappensWindow, Token);
        (await this.Settings.RemoveMute(new UserId(this.Guild.User1.Id), this.GuildId, this.Owner, Token)).ShouldSucceed();

        await this.RestartAsync();
        this._bot.Rest.CreateRole("Muted").ShouldBe(muteRoleId);
        await this.SpamAsync();

        Field(await this.WaitForSpamMuteAsync(), "Length").ShouldBe("2 minute(s)");
    }

    /// <summary>XC-121: a learned custom command answers both its text trigger and /Command after a restart.</summary>
    [Fact]
    public async Task CustomCommand_SurvivesARestart()
    {
        await this._bot.SetModuleAsync(Module.Commands, true);
        await this._bot.RunSlashCommandAndWaitAsync("commands", this.Guild.Admin, SlashOption.Subcommand("learn",
            SlashOption.String("name", "hello"), SlashOption.String("content", "Hello there!")));

        await this.RestartAsync();
        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "!hello");
        var reply = await this._bot.RunSlashCommandAndWaitAsync("command", this.Guild.User1, SlashOption.String("command_name", "hello"));

        reply.Message["content"]!.GetValue<string>().ShouldBe("Hello there!");
        var post = await this._bot.Rest.WaitForRequestAsync(request => request.Method == HttpMethod.Post
                                                                       && request.Path == $"channels/{this.Guild.General.Id}/messages");
        post.Json!["content"]!.GetValue<string>().ShouldBe("Hello there!");
    }

    private Task<FakeReply> SetModuleAsync(ModuleArgument module, bool enable)
        => this._bot.RunSlashCommandAndWaitAsync("modules", this.Guild.Admin,
            SlashOption.Subcommand("set", SlashOption.Enum("module", module), SlashOption.Boolean("enable", enable)));

    private Task<FakeReply> ModulesViewAsync()
        => this._bot.RunSlashCommandAndWaitAsync("modules", this.Guild.Admin, SlashOption.Subcommand("view"));

    private Task<int> EarnedCountAsync(FakeUser user, Func<int, bool> isDone, TimeSpan? window = null)
        => GrimoireTestBot.EventuallyAsync(async () =>
        {
            await using var dbContext = database.CreateDbContext();
            return await dbContext.XpHistory.OfType<EarnedXp>().CountAsync(xp => xp.UserId == new UserId(user.Id), Token);
        }, isDone, window);

    private async Task SpamAsync()
    {
        for (var i = 0; i < 6; i++)
        {
            this._bot.SendMessage(this.Guild.User1, this.Guild.General, "buy cheap gold now");
            await Task.Delay(TimeSpan.FromMilliseconds(30), Token);
        }
    }

    private Task<JsonNode> WaitForSpamMuteAsync()
        => this._bot.WaitForModLogEmbedAsync(embed => embed["author"]?["name"]?.GetValue<string>() == "Mute"
                                                       && embed["fields"] is not null);

    private static string Field(JsonNode embed, string name)
        => embed["fields"]!.AsArray().Single(field => field!["name"]!.GetValue<string>() == name)!["value"]!.GetValue<string>();
}
