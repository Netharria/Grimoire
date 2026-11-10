// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Enums;
using Grimoire.Features.Moderation.Lock;
using Grimoire.Features.Moderation.SinAdmin.Commands;
using Grimoire.Features.Shared.SharedDtos;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using Grimoire.Tests.Integration.FakeDiscord;
using Microsoft.Extensions.DependencyInjection;
using LogSetting = Grimoire.Features.Logging.Settings.LogSettingsCommands.Message.MessageLogSetting;
using ModuleArgument = Grimoire.Features.Shared.Commands.ModuleCommands.ModuleArguments;

namespace Grimoire.Tests.Integration.Shared;

/// <summary>
///     Settings cache invalidation, section F of <c>docs/testing/09-cross-cutting.md</c> (XC-97 to XC-105). Each test
///     reads a setting once so the cache holds it, changes it through a command, and checks the next read sees the
///     change. XC-104 and XC-105 edit the database directly, as a SQL edit would.
/// </summary>
[Collection("Test collection")]
public sealed class SettingsCacheTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private const string ModuleDisabledError = "This module is disabled in this server.";
    private static readonly TimeSpan NothingHappensWindow = TimeSpan.FromSeconds(2);

    private readonly GrimoireCoreFactory _database = database;
    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;
    private SettingsModule Settings => this._bot.Services.GetRequiredService<SettingsModule>();
    private GuildId GuildId => new(this.Guild.Id);
    private ModeratorId Owner => new(this.Guild.Owner.Id);
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await this._database.ResetDatabase();
        this._bot = await GrimoireTestBot.StartAsync(this._database);
        await this._bot.ConfigureModerationAsync();
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    /// <summary>XC-97: after the delete log moves, the next delete is logged in the new channel.</summary>
    [Fact]
    public async Task MovingTheDeleteLog_AppliesToTheNextDelete()
    {
        await this._bot.ConfigureMessageLogAsync();
        await this.SetDeleteLogAsync(this.Guild.Announcements);
        await this.SendAndDeleteAsync();
        await this.WaitForDeleteLogInAsync(this.Guild.Announcements);

        await this.SetDeleteLogAsync(this.Guild.ModLog);
        await this.SendAndDeleteAsync();

        await this.WaitForDeleteLogInAsync(this.Guild.ModLog);
    }

    /// <summary>XC-98: after /Mutes Set, both /Mute and the spam filter use the new role.</summary>
    [Fact]
    public async Task ChangingTheMuteRole_AppliesToTheNextMuteAndSpamMute()
    {
        await this._bot.SetModuleAsync(Module.AntiSpam, true);
        var roleA = this._bot.Rest.CreateRole("Muted A");
        var roleB = this._bot.Rest.CreateRole("Muted B");
        (await this.Settings.SetMuteRole(this.GuildId, this.Owner, new RoleId(roleA), cancellationToken: TestContext.Current.CancellationToken)).ShouldSucceed();
        await this.MuteAsync(this.Guild.User1);
        await this._bot.Rest.WaitForRequestAsync(this.IsRoleGrant(this.Guild.User1, roleA));
        await this._bot.RunSlashCommandAndWaitAsync("unmute", this.Guild.Admin, SlashOption.User("user", this.Guild.User1));

        (await this._bot.RunSlashCommandAndWaitAsync("mutes", this.Guild.Admin, SlashOption.Subcommand("set", SlashOption.Role("role", roleB))))
            .Description.ShouldBe($"Will now use role <@&{roleB}> for muting users.");
        await this.MuteAsync(this.Guild.User1);
        await this._bot.Rest.WaitForRequestAsync(this.IsRoleGrant(this.Guild.User1, roleB));
        await this._bot.RunSlashCommandAndWaitAsync("unmute", this.Guild.Admin, SlashOption.User("user", this.Guild.User1));
        for (var i = 0; i < 6; i++)
        {
            this._bot.SendMessage(this.Guild.User1, this.Guild.General, "buy cheap gold now");
            await Task.Delay(TimeSpan.FromMilliseconds(30), Token);
        }

        (await GrimoireTestBot.EventuallyAsync(() => Task.FromResult(this._bot.Rest.Requests.Count(this.IsRoleGrant(this.Guild.User1, roleB))),
            count => count >= 2)).ShouldBe(2, "the spam filter's mute uses role B as well");
        this._bot.Rest.Requests.Count(this.IsRoleGrant(this.Guild.User1, roleA)).ShouldBe(1);
        (await this.Settings.GetConfiguredMuteRole(this.GuildId, Token)).ShouldSucceed().ShouldBe(new RoleId(roleB));
    }

    /// <summary>
    ///     XC-100: when a thread lock expires, the expiry task removes it through <c>RemoveThreadLock</c>, which clears
    ///     the cache, so the next post stays.
    /// </summary>
    [Fact]
    public async Task AnExpiredThreadLock_StopsDeletingPostsAtOnce()
    {
        var thread = this.Guild.PublicThread;
        await this._bot.LockThreadAsync(thread, TimeSpan.FromSeconds(1));
        var first = this._bot.SendMessage(this.Guild.User1, thread, "deleted");
        await this._bot.Rest.WaitForRequestAsync(this.IsMessageDelete(thread, first));
        await Task.Delay(TimeSpan.FromSeconds(1.5), Token);

        await this._bot.RunBackgroundTickAsync<LockBackgroundTasks>();
        var second = this._bot.SendMessage(this.Guild.User1, thread, "stays");

        (await this._bot.Rest.ReceivesRequestWithinAsync(this.IsMessageDelete(thread, second), NothingHappensWindow)).ShouldBeFalse();
    }

    /// <summary>XC-101: a shorter auto-pardon window applies to the next /SinLog.</summary>
    [Fact]
    public async Task ShorteningAutoPardon_HidesOlderSinsInTheNextSinLog()
    {
        await using (var seed = this._database.CreateDbContext())
        {
            seed.Sins.Add(Sin.ForWarn(new ModerationActor.Moderator(new ModeratorId(this.Guild.Moderator.Id)),
                new UserId(this.Guild.User1.Id), this.GuildId, DateTimeOffset.UtcNow.AddDays(-3)).ShouldSucceed());
            await seed.SaveChangesAsync(Token);
        }

        (await this.SinLogAsync()).Description.ShouldNotBeNull().ShouldContain(" : Warn**");

        await this._bot.RunSlashCommandAndWaitAsync("mod_settings", this.Guild.Admin, SlashOption.Subcommand("auto_pardon",
            SlashOption.Enum("duration_type", Duration.Days), SlashOption.Integer("duration_amount", 1)));

        (await this.SinLogAsync()).Description.ShouldBe("That user does not have any logs");
    }

    /// <summary>XC-102: moving the user command channel makes /Level public in the new one and private in the old.</summary>
    [Fact]
    public async Task MovingTheUserCommandChannel_AppliesToTheNextLevel()
    {
        await this._bot.SetModuleAsync(Module.Leveling, true);
        (await this.Settings.SetUserCommandChannelSetting(this.GuildId, this.Owner, new ChannelId(this.Guild.General.Id), cancellationToken: TestContext.Current.CancellationToken))
            .ShouldBeSetOrUnchanged();
        (await this._bot.RunSlashCommandAndWaitAsync(this.Guild.General, "level", this.Guild.User1)).IsEphemeral.ShouldBeFalse();

        await this._bot.RunSlashCommandAndWaitAsync(this.Guild.ModLog, "general_settings", this.Guild.Admin,
            SlashOption.Subcommand("user_commands", SlashOption.Enum("option", ChannelOption.CurrentChannel)));

        (await this._bot.RunSlashCommandAndWaitAsync(this.Guild.ModLog, "level", this.Guild.User1)).IsEphemeral.ShouldBeFalse();
        (await this._bot.RunSlashCommandAndWaitAsync(this.Guild.General, "level", this.Guild.User1)).IsEphemeral.ShouldBeTrue();
    }

    /// <summary>XC-104: a setting changed directly in the database is not seen until the bot restarts.</summary>
    [Fact]
    public async Task ADirectDatabaseEdit_IsStaleUntilARestart()
    {
        await this._bot.SetModuleAsync(Module.Leveling, true);
        (await this.LevelAsync()).Description.ShouldNotBe(ModuleDisabledError);

        await this.DisableLevelingInTheDatabaseAsync();

        (await this.LevelAsync()).Description.ShouldNotBe(ModuleDisabledError, "the cache still says Leveling is on");
        await this._bot.DisposeAsync();
        this._bot = await GrimoireTestBot.StartAsync(this._database);
        (await this.LevelAsync()).Description.ShouldBe(ModuleDisabledError);
    }

    /// <summary>
    ///     XC-105: after a direct database edit, setting the value the database already holds should still clear the
    ///     stale cache. Today the write is "not modified", returns before clearing the cache, and the old value stays.
    /// </summary>
    [Fact(Skip = "Fails: a not-modified write returns before RemoveAsync, so the stale cached value stays. KI-103")]
    public async Task SettingTheValueTheDatabaseHolds_ClearsAStaleCache()
    {
        await this._bot.SetModuleAsync(Module.Leveling, true);
        (await this.LevelAsync()).Description.ShouldNotBe(ModuleDisabledError);
        await this.DisableLevelingInTheDatabaseAsync();

        await this._bot.RunSlashCommandAndWaitAsync("modules", this.Guild.Admin,
            SlashOption.Subcommand("set", SlashOption.Enum("module", ModuleArgument.Leveling), SlashOption.Boolean("enable", false)));

        (await this.LevelAsync()).Description.ShouldBe(ModuleDisabledError);
    }

    private async Task DisableLevelingInTheDatabaseAsync()
    {
        await using var settingsContext = this._database.CreateSettingsDbContext();
        settingsContext.GuildSettings.Add(GuildSettingDisabled.Create(Module.Leveling.ToGuildSettingType()!.Value, this.GuildId,
            this.Owner, DateTimeOffset.UtcNow).ShouldSucceed());
        await settingsContext.SaveChangesAsync(Token);
    }

    private Task<FakeReply> LevelAsync() => this._bot.RunSlashCommandAndWaitAsync("level", this.Guild.User1);

    private Task<FakeReply> SinLogAsync()
        => this._bot.RunSlashCommandAndWaitAsync("sin_log", this.Guild.Moderator,
            SlashOption.Enum("type", SinQueryType.All), SlashOption.User("user", this.Guild.User1));

    private async Task MuteAsync(FakeUser target)
        => (await this._bot.RunSlashCommandAndWaitAsync("mute", this.Guild.Admin, SlashOption.User("user", target),
                SlashOption.Enum("duration_type", DurationType.Hours), SlashOption.Integer("duration_amount", 1)))
            .IsEphemeral.ShouldBeFalse();

    private Task<FakeReply> SetDeleteLogAsync(FakeChannel channel)
        => this._bot.RunSlashCommandAndWaitAsync("log", this.Guild.Admin, SlashOption.Group("message", SlashOption.Subcommand("set",
            SlashOption.Enum("setting", LogSetting.DeleteLog), SlashOption.Enum("option", ChannelOption.SelectChannel),
            SlashOption.Channel("value", channel))));

    private async Task SendAndDeleteAsync()
    {
        var messageId = this._bot.SendMessage(this.Guild.User1, this.Guild.General, "logged then deleted");
        await GrimoireTestBot.EventuallyAsync(async () =>
        {
            await using var dbContext = this._database.CreateDbContext();
            return await dbContext.Messages.AnyAsync(message => message.Id == new MessageId(messageId), Token);
        }, saved => saved);
        this._bot.DeleteMessage(messageId, this.Guild.General);
    }

    private Task<RecordedRequest> WaitForDeleteLogInAsync(FakeChannel channel)
        => this._bot.Rest.WaitForRequestAsync(request => request.Method == HttpMethod.Post
                                                         && request.Path == $"channels/{channel.Id}/messages"
                                                         && request.Json?["embeds"]?[0]?["author"]?["name"]?.GetValue<string>()
                                                         == $"Message deleted in #{this.Guild.General.Name}");

    private Func<RecordedRequest, bool> IsRoleGrant(FakeUser user, ulong roleId)
        => request => request.Method == HttpMethod.Put
                      && request.Path == $"guilds/{this.Guild.Id}/members/{user.Id}/roles/{roleId}";

    private Func<RecordedRequest, bool> IsMessageDelete(FakeChannel channel, ulong messageId)
        => request => request.Method == HttpMethod.Delete && request.Path == $"channels/{channel.Id}/messages/{messageId}";
}
