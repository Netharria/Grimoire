// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text.Json.Nodes;
using Grimoire.Features.Moderation.Lock;
using Grimoire.Features.Moderation.Mute;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using Grimoire.Tests.Integration.FakeDiscord;
using Microsoft.Extensions.DependencyInjection;
using ModuleArgument = Grimoire.Features.Shared.Commands.ModuleCommands.ModuleArguments;

namespace Grimoire.Tests.Integration.Shared;

/// <summary>
///     The module-toggle matrix, section E of <c>docs/testing/09-cross-cutting.md</c>: what stops, and what keeps
///     working, while a module is off (XC-74 to XC-91).
/// </summary>
[Collection("Test collection")]
public sealed class ModuleToggleTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private const string ModuleDisabledError = "This module is disabled in this server.";
    private const ulong TextLock = (1UL << 11) | (1UL << 38) | (1UL << 12) | (1UL << 6);
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
        this._bot = await GrimoireTestBot.StartAsync(database);
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    /// <summary>XC-74: with Leveling off, messages earn nothing and /UserInfo leaves out the level fields.</summary>
    [Fact]
    public async Task LevelingOff_EarnsNoXp_AndUserInfoHasNoLevelFields()
    {
        await this._bot.ConfigureModerationAsync();
        await this._bot.SetModuleAsync(Module.Leveling, false);

        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "hello");

        (await this.XpOfAsync(this.Guild.User1, NothingHappensWindow)).ShouldBe(0);
        var embed = (await this._bot.RunSlashCommandAndWaitAsync("user_info", this.Guild.Admin,
            SlashOption.User("user", this.Guild.User1))).Embed!;
        FieldNames(embed).ShouldNotContain("Level");
        FieldNames(embed).ShouldNotContain("Can Gain Xp");
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>
    ///     XC-91: the event reads the same cached module flag the commands do, so turning Leveling off after it was
    ///     read stops XP straight away. The second sender has no timeout running.
    /// </summary>
    [Fact]
    public async Task DisablingLeveling_AfterItWasRead_StopsXpAtOnce()
    {
        await this._bot.SetModuleAsync(Module.Leveling, true);
        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "first");
        (await this.XpOfAsync(this.Guild.User1)).ShouldBeGreaterThan(0);

        await this.SetModuleByCommandAsync(ModuleArgument.Leveling, false);
        this._bot.SendMessage(this.Guild.Moderator, this.Guild.General, "second");

        (await this.XpOfAsync(this.Guild.Moderator, NothingHappensWindow)).ShouldBe(0);
    }

    /// <summary>
    ///     XC-75 and XC-76: a nickname change while UserLog is off is neither stored nor logged. Once UserLog is back
    ///     on, the next change logs the value stored before it was turned off as "Before", not the one set while off.
    /// </summary>
    [Fact]
    public async Task UserLogOff_StoresAndLogsNoNicknameChange_AndTheNextLogShowsTheOlderValue()
    {
        await this._bot.ConfigureModerationAsync();
        await this._bot.SetModuleAsync(Module.UserLog, false);
        (await this.Settings.SetLogChannelSetting(GuildLogType.NicknameUpdated, this.GuildId, this.Owner,
            new ChannelId(this.Guild.ModLog.Id), cancellationToken: TestContext.Current.CancellationToken)).ShouldBeSetOrUnchanged();
        await this.SeedNicknameAsync(this.Guild.User1, "first");

        this._bot.UpdateProfile(this.Guild.User1, nickname: "second");

        (await this._bot.Rest.ReceivesRequestWithinAsync(request => this._bot.IsModLogPost(request)
                && request.Json?["embeds"]?[0]?["author"]?["name"]?.GetValue<string>() == "Nickname Updated",
            NothingHappensWindow)).ShouldBeFalse();
        (await this.NicknamesOfAsync(this.Guild.User1)).ShouldNotContain("second");

        await this._bot.SetModuleAsync(Module.UserLog, true);
        this._bot.UpdateProfile(this.Guild.User1, nickname: "third");

        var embed = await this._bot.WaitForModLogEmbedAsync(e => e["author"]?["name"]?.GetValue<string>() == "Nickname Updated");
        Field(embed, "Before").ShouldBe("first");
        Field(embed, "After").ShouldBe("third");
    }

    /// <summary>
    ///     XC-78: a message saved while MessageLog was on and deleted while it is off gets no delete entry and no log.
    ///     Turning the log back on finds the row still there, with no delete entry.
    /// </summary>
    [Fact]
    public async Task MessageLogOff_ADeleteOfASavedMessage_IsNotRecorded()
    {
        await this._bot.ConfigureModerationAsync();
        await this._bot.ConfigureMessageLogAsync();
        var messageId = this._bot.SendMessage(this.Guild.User1, this.Guild.General, "saved while on");
        (await GrimoireTestBot.EventuallyAsync(() => this.MessageSavedAsync(messageId), saved => saved)).ShouldBeTrue();

        await this._bot.SetModuleAsync(Module.MessageLog, false);
        this._bot.DeleteMessage(messageId, this.Guild.General);

        (await this._bot.Rest.ReceivesRequestWithinAsync(this._bot.IsModLogPost, NothingHappensWindow)).ShouldBeFalse();
        await this._bot.SetModuleAsync(Module.MessageLog, true);
        (await this.MessageSavedAsync(messageId)).ShouldBeTrue();
        await using var dbContext = database.CreateDbContext();
        (await dbContext.MessageHistory.OfType<MessageDeletedEntry>()
            .AnyAsync(entry => entry.MessageId == new MessageId(messageId), Token)).ShouldBeFalse();
    }

    /// <summary>XC-79: with Moderation off, a ban and unban from Discord's client are neither recorded nor logged.</summary>
    [Fact]
    public async Task ModerationOff_ClientBanAndUnban_LeaveNoSinAndNoLog()
    {
        (await this.Settings.SetLogChannelSetting(GuildLogType.Moderation, this.GuildId, this.Owner,
            new ChannelId(this.Guild.ModLog.Id), cancellationToken: TestContext.Current.CancellationToken)).ShouldBeSetOrUnchanged();
        await this._bot.SetModuleAsync(Module.Moderation, false);

        this._bot.BanFromClient(this.Guild.Admin, this.Guild.User1, "client ban");
        await Task.Delay(NothingHappensWindow, Token);
        this._bot.UnbanFromClient(this.Guild.Admin, this.Guild.User1);

        (await this._bot.Rest.ReceivesRequestWithinAsync(this._bot.IsModLogPost, NothingHappensWindow)).ShouldBeFalse();
        await using var dbContext = database.CreateDbContext();
        (await dbContext.Sins.AnyAsync(Token)).ShouldBeFalse();
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>XC-82: turning Moderation off does not stop running mutes and locks from expiring and being logged.</summary>
    [Fact]
    public async Task ModerationOff_MutesAndLocksStillExpire()
    {
        await this._bot.ConfigureModerationAsync();
        var muteRoleId = this._bot.Rest.CreateRole("Muted");
        (await this.Settings.SetMuteRole(this.GuildId, this.Owner, new RoleId(muteRoleId), cancellationToken: TestContext.Current.CancellationToken)).ShouldSucceed();
        await this.SeedExpiredMuteAsync(this.Guild.User1, muteRoleId);
        await this.SeedExpiredChannelLockAsync(this.Guild.General);
        await this._bot.SetModuleAsync(Module.Moderation, false);

        await this._bot.RunBackgroundTickAsync<MuteBackgroundTasks>();
        await this._bot.RunBackgroundTickAsync<LockBackgroundTasks>();

        this.Guild.GetMember(this.Guild.User1).RoleIds.ShouldNotContain(muteRoleId);
        this.Guild.OverwriteOf(this.Guild.General.Id, this.Guild.Id).ShouldBe((0UL, 0UL));
        await this._bot.WaitForModLogEmbedAsync(e => e["description"]?.GetValue<string>()
            == $"Mute on {this.Guild.User1.MemberMention} has expired.");
        await this._bot.WaitForModLogEmbedAsync(e => e["description"]?.GetValue<string>()
            == $"Lock on {this.Guild.General.Mention} has expired.");
        await using var settingsContext = database.CreateSettingsDbContext();
        (await settingsContext.Mutes.OfType<MuteRemoved>().CountAsync(Token)).ShouldBe(1);
        (await settingsContext.ChannelLocks.OfType<ChannelUnlocked>().CountAsync(Token)).ShouldBe(1);
    }

    /// <summary>XC-85: with Commands off, a learned command answers neither its text trigger nor /Command.</summary>
    [Fact]
    public async Task CommandsOff_NeitherTheTextTriggerNorTheSlashCommandAnswers()
    {
        await this._bot.SetModuleAsync(Module.Commands, true);
        await this._bot.RunSlashCommandAndWaitAsync("commands", this.Guild.Admin, SlashOption.Subcommand("learn",
            SlashOption.String("name", "hello"), SlashOption.String("content", "Hello there!")));
        await this._bot.SetModuleAsync(Module.Commands, false);

        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "!hello");
        var reply = await this._bot.RunSlashCommandAndWaitAsync("command", this.Guild.User1, SlashOption.String("command_name", "hello"));

        reply.Description.ShouldBe(ModuleDisabledError);
        (await this._bot.Rest.ReceivesRequestWithinAsync(request => request.Method == HttpMethod.Post
                && request.Path == $"channels/{this.Guild.General.Id}/messages", NothingHappensWindow))
            .ShouldBeFalse();
        await using var dbContext = database.CreateDbContext();
        (await dbContext.CustomCommandUsages.AnyAsync(Token)).ShouldBeFalse();
    }

    /// <summary>XC-87: with every module off, /Modules Set still posts to the mod log, which hangs on General.</summary>
    [Fact]
    public async Task EveryModuleOff_ModulesSet_IsStillLogged()
    {
        (await this.Settings.SetLogChannelSetting(GuildLogType.Moderation, this.GuildId, this.Owner,
            new ChannelId(this.Guild.ModLog.Id), cancellationToken: TestContext.Current.CancellationToken)).ShouldBeSetOrUnchanged();

        (await this.SetModuleByCommandAsync(ModuleArgument.Leveling, true)).Description.ShouldBe("Enabled Leveling");

        await this._bot.WaitForModLogEmbedAsync(embed =>
            embed["description"]?.GetValue<string>() == $"{this.Guild.Admin.MemberMention} Enabled Leveling");
    }

    /// <summary>XC-88: a guild that was just added has no module rows, and every module reads as off.</summary>
    [Fact]
    public async Task NewGuild_HasNoModuleRows_AndEveryModuleIsOff()
    {
        var reply = await this._bot.RunSlashCommandAndWaitAsync("modules", this.Guild.Admin, SlashOption.Subcommand("view"));

        reply.Description.ShouldBe(
            "**Leveling Enabled:** False\n**User Log Enabled:** False\n**Message Log Enabled:** False\n" +
            "**Moderation Enabled:** False\n**Commands Enabled:** False\n**Anti Spam Enabled:** False\n");
        await using var settingsContext = database.CreateSettingsDbContext();
        (await settingsContext.GuildSettings.AnyAsync(setting => setting.GuildId == this.GuildId, Token)).ShouldBeFalse();
    }

    private Task<FakeReply> SetModuleByCommandAsync(ModuleArgument module, bool enable)
        => this._bot.RunSlashCommandAndWaitAsync("modules", this.Guild.Admin,
            SlashOption.Subcommand("set", SlashOption.Enum("module", module), SlashOption.Boolean("enable", enable)));

    private async Task<long> XpOfAsync(FakeUser user, TimeSpan? window = null)
        => await GrimoireTestBot.EventuallyAsync(async () =>
        {
            await using var dbContext = database.CreateDbContext();
            return (await dbContext.XpHistory.OfType<EarnedXp>().Where(xp => xp.UserId == new UserId(user.Id)).ToListAsync(Token))
                .Sum(xp => xp.Xp.Value);
        }, xp => xp > 0, window);

    private async Task<bool> MessageSavedAsync(ulong messageId)
    {
        await using var dbContext = database.CreateDbContext();
        return await dbContext.Messages.AnyAsync(message => message.Id == new MessageId(messageId), Token);
    }

    private async Task SeedNicknameAsync(FakeUser user, string nickname)
    {
        await using var seed = database.CreateDbContext();
        seed.NicknameHistory.Add(new NicknameHistory
        {
            UserId = new UserId(user.Id),
            GuildId = this.GuildId,
            Nickname = Nickname.Create(nickname).ShouldSucceed(),
            Timestamp = DateTimeOffset.UtcNow.AddSeconds(1)
        });
        await seed.SaveChangesAsync(Token);
    }

    private async Task<List<string>> NicknamesOfAsync(FakeUser user)
    {
        await using var dbContext = database.CreateDbContext();
        return [.. (await dbContext.NicknameHistory.Where(row => row.UserId == new UserId(user.Id)).ToListAsync(Token))
            .Select(row => row.Nickname?.Value).OfType<string>()];
    }

    private async Task SeedExpiredMuteAsync(FakeUser user, ulong roleId)
    {
        var now = DateTimeOffset.UtcNow;
        var mute = MuteAdded.Create(new UserId(user.Id), this.GuildId, new ModeratorId(this.Guild.Moderator.Id), new SinId(1),
            now.AddMinutes(-10), now.AddMinutes(-1), new RoleId(roleId)).ShouldSucceed();
        await using (var settingsContext = database.CreateSettingsDbContext())
        {
            settingsContext.Mutes.Add(mute);
            await settingsContext.SaveChangesAsync(Token);
        }

        this._bot.SetRolesByHand(user, [.. this.Guild.GetMember(user).RoleIds, roleId]);
        await Task.Delay(TimeSpan.FromMilliseconds(200), Token);
    }

    private async Task SeedExpiredChannelLockAsync(FakeChannel channel)
    {
        var now = DateTimeOffset.UtcNow;
        var channelLock = ChannelLocked.Create(new ModeratorId(this.Guild.Moderator.Id), null, new ChannelId(channel.Id),
            this.GuildId, now.AddMinutes(-10), new PreviouslyAllowedPermissions(0), new PreviouslyDeniedPermissions(0),
            now.AddMinutes(-1)).ShouldSucceed();
        await using (var settingsContext = database.CreateSettingsDbContext())
        {
            settingsContext.ChannelLocks.Add(channelLock);
            await settingsContext.SaveChangesAsync(Token);
        }

        this.Guild.SetOverwrite(channel.Id, this.Guild.Id, 0, 0, TextLock);
        this._bot.Gateway.Dispatch("CHANNEL_UPDATE", this.Guild.ChannelJson(channel));
        await Task.Delay(TimeSpan.FromMilliseconds(150), Token);
    }

    private static string[] FieldNames(JsonNode embed)
        => [.. embed["fields"]!.AsArray().Select(field => field!["name"]!.GetValue<string>())];

    private static string Field(JsonNode embed, string name)
        => embed["fields"]!.AsArray().Single(field => field!["name"]!.GetValue<string>() == name)!["value"]!.GetValue<string>();
}
