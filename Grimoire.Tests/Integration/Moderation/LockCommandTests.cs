// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Globalization;
using Grimoire.Features.Shared.GuildIssues;
using Grimoire.Features.Shared.SharedDtos;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using Grimoire.Tests.Integration.FakeDiscord;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Tests.Integration.Moderation;

/// <summary>
///     <c>/Lock</c> and <c>/Unlock</c>, cases MOD-144 to MOD-158, MOD-163, MOD-164 and MOD-169 to MOD-177 in
///     <c>docs/testing/02-moderation-actions.md</c>. A channel lock edits the <c>@everyone</c> overwrite, whose id is
///     the guild's. Thread enforcement is in <c>ThreadLockTests</c>, expiry (MOD-178 onward) in A-11, and MOD-176
///     (two guilds) in A-35.
/// </summary>
[Collection("Test collection")]
public sealed class LockCommandTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private const ulong SendMessages = 1UL << 11;
    private const ulong SendTtsMessages = 1UL << 12;
    private const ulong AddReactions = 1UL << 6;
    private const ulong SendMessagesInThreads = 1UL << 38;
    private const ulong AttachFiles = 1UL << 15;
    private const ulong EmbedLinks = 1UL << 14;
    private const ulong TextLock = SendMessages | SendMessagesInThreads | SendTtsMessages | AddReactions;

    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;
    private SettingsModule Settings => this._bot.Services.GetRequiredService<SettingsModule>();

    public async ValueTask InitializeAsync()
    {
        await database.ResetDatabase();
        this._bot = await GrimoireTestBot.StartAsync(database);
        await this._bot.ConfigureModerationAsync();
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    // ── /Lock ─────────────────────────────────────────────────────────────────

    /// <summary>MOD-144: locking the current channel denies the lock bits to @everyone, records it, and logs it.</summary>
    [Fact]
    public async Task Lock_DeniesTheLockPermissions_RecordsAndLogsIt()
    {
        var before = DateTimeOffset.UtcNow;

        (await this.LockAsync(this.Guild.General, DurationType.Minutes, 2)).Description
            .ShouldBe($"{this.Guild.General.Mention} has been locked for 2 Minutes");

        this.EveryoneOverwrite(this.Guild.General).ShouldBe((0UL, TextLock));
        var channelLock = await this.ActiveLockAsync(this.Guild.General);
        channelLock.EndTime.ShouldBeInRange(before.AddMinutes(2), DateTimeOffset.UtcNow.AddMinutes(2));
        channelLock.PreviouslyAllowed.Permissions.ShouldBe(0);
        channelLock.PreviouslyDenied.Permissions.ShouldBe(0);
        await this._bot.WaitForModLogEmbedAsync(embed => embed["description"]?.GetValue<string>()
            == $"{this.Guild.General.Mention} has been locked for 2 Minutes by {this.Guild.Moderator.MemberMention}");
        this._bot.Rest.Unhandled.ShouldBeEmpty();
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>MOD-145.</summary>
    [Fact]
    public async Task Lock_WithAReason_RecordsAndLogsIt()
    {
        await this.LockAsync(this.Guild.General, DurationType.Minutes, 2, reason: "raid");

        (await this.ActiveLockAsync(this.Guild.General)).Reason!.Value.Value.ShouldBe("raid");
        await this._bot.WaitForModLogEmbedAsync(embed =>
            embed["description"]?.GetValue<string>().EndsWith(" for raid", StringComparison.Ordinal) == true);
    }

    /// <summary>MOD-146: the channel option locks another channel.</summary>
    [Fact]
    public async Task Lock_WithAChannelOption_LocksThatChannel()
    {
        await this.LockAsync(this.Guild.General, DurationType.Minutes, 2, this.Guild.ModLog);

        this.EveryoneOverwrite(this.Guild.ModLog).ShouldBe((0UL, TextLock));
        this.EveryoneOverwrite(this.Guild.General).ShouldBeNull();
    }

    /// <summary>MOD-147: existing bits are kept, an allowed lock bit flips to denied and is remembered.</summary>
    [Fact]
    public async Task Lock_KeepsUnrelatedBits_AndRemembersAllowedLockBits()
    {
        await this.SetEveryoneOverwriteAsync(this.Guild.General, SendMessages, AttachFiles);

        await this.LockAsync(this.Guild.General, DurationType.Minutes, 2);

        this.EveryoneOverwrite(this.Guild.General).ShouldBe((0UL, AttachFiles | TextLock));
        (await this.ActiveLockAsync(this.Guild.General)).PreviouslyAllowed.Permissions.ShouldBe((long)SendMessages);
    }

    /// <summary>MOD-148, MOD-149 and MOD-153: unlock restores the lock bits exactly and leaves everything else alone.</summary>
    [Fact]
    public async Task Unlock_RestoresThePreLockState_AndKeepsLaterUnrelatedChanges()
    {
        await this.SetEveryoneOverwriteAsync(this.Guild.General, SendMessages, AddReactions);
        await this.LockAsync(this.Guild.General, DurationType.Minutes, 2);
        var (allowed, denied) = this.EveryoneOverwrite(this.Guild.General)!.Value;
        await this.SetEveryoneOverwriteAsync(this.Guild.General, allowed, denied | EmbedLinks);

        (await this.UnlockAsync(this.Guild.General)).Description.ShouldBe($"{this.Guild.General.Mention} has been unlocked");

        this.EveryoneOverwrite(this.Guild.General).ShouldBe((SendMessages, AddReactions | EmbedLinks));
        await using var settingsContext = database.CreateSettingsDbContext();
        (await settingsContext.ChannelLocks.OfType<ChannelUnlocked>().CountAsync(TestContext.Current.CancellationToken)).ShouldBe(1);
        await this._bot.WaitForModLogEmbedAsync(embed => embed["description"]?.GetValue<string>()
            == $"{this.Guild.Moderator.MemberMention} unlocked {this.Guild.General.Mention}");
    }

    /// <summary>MOD-150.</summary>
    [Fact]
    public async Task Unlock_OfAChannelThatIsNotLocked_SaysSo()
        => (await this.UnlockAsync(this.Guild.General)).Description.ShouldBe($"{this.Guild.General.Mention} is not locked.");

    /// <summary>MOD-151 and MOD-152: locking again keeps the original pre-lock state, so unlock restores that.</summary>
    [Fact]
    public async Task LockingTwice_KeepsTheOriginalState_ForTheUnlock()
    {
        await this.SetEveryoneOverwriteAsync(this.Guild.General, SendMessages, AttachFiles);
        await this.LockAsync(this.Guild.General, DurationType.Minutes, 2);

        (await this.LockAsync(this.Guild.General, DurationType.Hours, 1)).Description
            .ShouldBe($"{this.Guild.General.Mention} has been locked for 1 Hours");
        (await this.ActiveLockAsync(this.Guild.General)).PreviouslyAllowed.Permissions.ShouldBe((long)SendMessages);

        await this.UnlockAsync(this.Guild.General);
        this.EveryoneOverwrite(this.Guild.General).ShouldBe((SendMessages, AttachFiles));
    }

    /// <summary>MOD-154 and MOD-155: categories and forums are locked the same way.</summary>
    [Theory]
    [InlineData("category")]
    [InlineData("forum")]
    public async Task Lock_WorksOnCategoriesAndForums(string kind)
    {
        var channel = kind == "category" ? this.Guild.Category : this.Guild.Forum;

        await this.LockAsync(this.Guild.General, DurationType.Minutes, 2, channel);

        this.EveryoneOverwrite(channel).ShouldBe((0UL, TextLock));
        (await this.ActiveLockAsync(channel)).ShouldNotBeNull();
    }

    /// <summary>
    ///     M-8: a thread lock deletes every new message, so without Manage Messages it would be recorded and then fail
    ///     on every message. It's refused instead.
    /// </summary>
    [Fact]
    public async Task Lock_OfAThread_WithoutManageMessages_IsRefused()
    {
        await this._bot.DisposeAsync();
        this._bot = await GrimoireTestBot.StartAsync(database, guild => guild.BotPermissions =
            FakeGuild.ViewChannelPermission | FakeGuild.SendMessagesPermission | FakeGuild.EmbedLinksPermission
            | FakeGuild.ManageChannelsPermission | FakeGuild.ManageRolesPermission);
        await this._bot.ConfigureModerationAsync();

        var reply = await this.LockAsync(this.Guild.General, DurationType.Minutes, 2, this.Guild.PublicThread);

        reply.Description.ShouldBe($"{this.Guild.Bot.MemberMention} needs **Manage Messages** in {this.Guild.PublicThread.Mention} to lock it.");
        (await this.Settings.IsThreadLocked(new ChannelId(this.Guild.PublicThread.Id), new GuildId(this.Guild.Id),
            TestContext.Current.CancellationToken)).ShouldSucceed().ShouldBeFalse();
    }

    /// <summary>
    ///     Review M-4: Discord checks the moderator only where the command is run, so the bot checks the target. A
    ///     moderator without Manage Messages there can't lock it.
    /// </summary>
    [Fact]
    public async Task Lock_OfAChannelTheModeratorCantManage_IsRefused()
    {
        await this.DenyModeratorManageMessagesAsync(this.Guild.ModLog);

        (await this.LockAsync(this.Guild.General, DurationType.Minutes, 2, this.Guild.ModLog)).Description
            .ShouldBe($"You need **Manage Messages** in {this.Guild.ModLog.Mention} to lock or unlock it.");

        this.EveryoneOverwrite(this.Guild.ModLog).ShouldBeNull();
        (await this.Settings.GetActiveChannelLockAsync(new ChannelId(this.Guild.ModLog.Id), new GuildId(this.Guild.Id)))
            .ShouldBeOfType<Result<ChannelLocked>.NotFound>();
    }

    /// <summary>Review M-4: nor can they lift another moderator's lock there.</summary>
    [Fact]
    public async Task Unlock_OfAChannelTheModeratorCantManage_IsRefused()
    {
        await this.LockAsync(this.Guild.General, DurationType.Minutes, 2, this.Guild.ModLog);
        await this.DenyModeratorManageMessagesAsync(this.Guild.ModLog);

        (await this.UnlockAsync(this.Guild.ModLog)).Description
            .ShouldBe($"You need **Manage Messages** in {this.Guild.ModLog.Mention} to lock or unlock it.");

        this.EveryoneOverwrite(this.Guild.ModLog).ShouldBe((0UL, TextLock));
        (await this.ActiveLockAsync(this.Guild.ModLog)).ShouldNotBeNull();
    }

    /// <summary>MOD-156, MOD-158 and MOD-169: Discord's client enforces these from the registration.</summary>
    /// <remarks>Cases: MOD-157 (a zero duration is refused by the registered minimum of 1, since KI-07).</remarks>
    [Fact]
    public void LockAndUnlock_AreRegisteredWithTheirLimits()
    {
        var lockCommand = this._bot.Rest.RegisteredCommands["lock"];
        var options = lockCommand["options"]!.AsArray();
        options.Single(o => o!["name"]!.GetValue<string>() == "channel")!["channel_types"]!.AsArray()
            .Select(type => type!.GetValue<int>()).ShouldBe([0, 11, 12, 4, 15], ignoreOrder: true);
        options.Single(o => o!["name"]!.GetValue<string>() == "duration_amount")!["min_value"]!.GetValue<int>().ShouldBe(1);
        var manageMessages = FakeGuild.ManageMessagesPermission.ToString(CultureInfo.InvariantCulture);
        lockCommand["default_member_permissions"]!.ToString().ShouldBe(manageMessages);
        this._bot.Rest.RegisteredCommands["unlock"]["default_member_permissions"]!.ToString().ShouldBe(manageMessages);
    }

    /// <summary>MOD-171: owner and Administrator can lock; Discord gates everyone else on Manage Messages.</summary>
    [Theory]
    [InlineData("owner")]
    [InlineData("admin")]
    public async Task Lock_ByTheOwnerOrAnAdmin_Works(string who)
    {
        var user = who == "owner" ? this.Guild.Owner : this.Guild.Admin;

        (await this._bot.RunSlashCommandAndWaitAsync(this.Guild.General, "lock", user,
                SlashOption.Enum("duration_type", DurationType.Minutes), SlashOption.Integer("duration_amount", 2)))
            .Description.ShouldBe($"{this.Guild.General.Mention} has been locked for 2 Minutes");
    }

    /// <summary>MOD-172.</summary>
    [Theory]
    [InlineData("lock")]
    [InlineData("unlock")]
    public async Task WhenTheBotLacksManageChannels_IsRefused(string command)
    {
        await this.RestartBotWithAsync(FakeGuild.ManageRolesPermission);

        var reply = command == "lock"
            ? await this.LockAsync(this.Guild.General, DurationType.Minutes, 2)
            : await this.UnlockAsync(this.Guild.General);

        reply.Description.ShouldBe("The bot did not have the needed permissions to execute this command.");
        this.EveryoneOverwrite(this.Guild.General).ShouldBeNull();
    }

    /// <summary>
    ///     MOD-173 and MOD-174: Discord refuses the overwrite without Manage Roles or channel access, and the error
    ///     handler explains it.
    /// </summary>
    /// <remarks>Cases: ALRT-43 (the "Discord refused" reply).</remarks>
    [Fact]
    public async Task Lock_ThatDiscordRefuses_ShowsTheMissingPermissionsHelp()
    {
        await this.RestartBotWithAsync(FakeGuild.ManageChannelsPermission | FakeGuild.ManageRolesPermission);
        var channel = this.Guild.Private;
        var issue = GuildIssueCatalog.BotMissingPermissions(new GuildId(this.Guild.Id), "Lock");

        (await this.LockAsync(this.Guild.General, DurationType.Minutes, 2, channel)).Description
            .ShouldBe($"{issue.Summary}\n**Suggested fix:** {issue.SuggestedFix}");
    }

    /// <summary>MOD-173 and MOD-174: a refused lock should leave no lock recorded.</summary>
    [Fact]
    public async Task Lock_ThatDiscordRefuses_RecordsNothing()
    {
        await this.RestartBotWithAsync(FakeGuild.ManageChannelsPermission | FakeGuild.ManageRolesPermission);

        await this.LockAsync(this.Guild.General, DurationType.Minutes, 2, this.Guild.Private);

        (await this.Settings.GetActiveChannelLockAsync(new ChannelId(this.Guild.Private.Id), new GuildId(this.Guild.Id), TestContext.Current.CancellationToken))
            .ShouldBeOfType<Result<ChannelLocked>.NotFound>();
    }

    /// <summary>MOD-177: overwrites need Manage Roles, so a bot without it is stopped before anything changes.</summary>
    [Fact]
    public async Task Lock_WithoutManageRoles_IsRefused()
    {
        await this.RestartBotWithAsync(FakeGuild.ManageChannelsPermission);

        (await this.LockAsync(this.Guild.General, DurationType.Minutes, 2)).Description
            .ShouldBe("The bot did not have the needed permissions to execute this command.");

        this.EveryoneOverwrite(this.Guild.General).ShouldBeNull();
    }

    /// <summary>MOD-175.</summary>
    [Theory]
    [InlineData("lock")]
    [InlineData("unlock")]
    public async Task WithModerationDisabled_IsRefused(string command)
    {
        await this._bot.SetModuleAsync(Module.Moderation, false);

        var reply = command == "lock"
            ? await this.LockAsync(this.Guild.General, DurationType.Minutes, 2)
            : await this.UnlockAsync(this.Guild.General);

        reply.Description.ShouldBe("This module is disabled in this server.");
    }

    // ── Threads ───────────────────────────────────────────────────────────────

    /// <summary>MOD-159, MOD-163 and MOD-164: the thread lock and unlock replies and records.</summary>
    [Fact]
    public async Task ThreadLockAndUnlock_ReplyAndRecord()
    {
        var thread = this.Guild.PublicThread;

        (await this.LockAsync(this.Guild.General, DurationType.Minutes, 2, thread)).Description
            .ShouldBe($"{thread.Mention} has been locked for 2 Minutes");
        (await this.Settings.IsThreadLocked(new ChannelId(thread.Id), new GuildId(this.Guild.Id), TestContext.Current.CancellationToken)).ShouldSucceed().ShouldBeTrue();

        (await this.UnlockAsync(thread)).Description.ShouldBe($"{thread.Mention} has been unlocked");
        (await this.Settings.IsThreadLocked(new ChannelId(thread.Id), new GuildId(this.Guild.Id), TestContext.Current.CancellationToken)).ShouldSucceed().ShouldBeFalse();

        (await this.UnlockAsync(thread)).Description.ShouldBe($"{thread.Mention} is not locked.");
    }

    private async Task RestartBotWithAsync(ulong extraBotPermissions)
    {
        await this._bot.DisposeAsync();
        this._bot = await GrimoireTestBot.StartAsync(database, guild => guild.BotPermissions =
            FakeGuild.ViewChannelPermission | FakeGuild.SendMessagesPermission | FakeGuild.EmbedLinksPermission | extraBotPermissions);
        await this._bot.ConfigureModerationAsync();
    }

    private Task<FakeReply> LockAsync(FakeChannel from, DurationType type, int amount, FakeChannel? channel = null, string? reason = null)
        => this._bot.RunSlashCommandAndWaitAsync(from, "lock", this.Guild.Moderator,
        [
            SlashOption.Enum("duration_type", type), SlashOption.Integer("duration_amount", amount),
            .. channel is null ? Array.Empty<SlashOption>() : [SlashOption.Channel("channel", channel)],
            .. reason is null ? Array.Empty<SlashOption>() : [SlashOption.String("reason", reason)]
        ]);

    private Task<FakeReply> UnlockAsync(FakeChannel channel)
        => this._bot.RunSlashCommandAndWaitAsync(this.Guild.General, "unlock", this.Guild.Moderator, SlashOption.Channel("channel", channel));

    private (ulong Allow, ulong Deny)? EveryoneOverwrite(FakeChannel channel) => this.Guild.OverwriteOf(channel.Id, this.Guild.Id);

    /// <summary>Sets the @everyone overwrite as a moderator would by hand, and lets the bot's cache see it.</summary>
    private async Task SetEveryoneOverwriteAsync(FakeChannel channel, ulong allow, ulong deny)
    {
        this.Guild.SetOverwrite(channel.Id, this.Guild.Id, 0, allow, deny);
        this._bot.Gateway.Dispatch("CHANNEL_UPDATE", this.Guild.ChannelJson(channel));
        await Task.Delay(TimeSpan.FromMilliseconds(150), TestContext.Current.CancellationToken);
    }

    /// <summary>Takes Manage Messages from the moderator role in one channel, as for a moderator of other channels.</summary>
    private async Task DenyModeratorManageMessagesAsync(FakeChannel channel)
    {
        this.Guild.SetOverwrite(channel.Id, this.Guild.ModeratorRoleId, 0, 0, FakeGuild.ManageMessagesPermission);
        this._bot.Gateway.Dispatch("CHANNEL_UPDATE", this.Guild.ChannelJson(channel));
        await Task.Delay(TimeSpan.FromMilliseconds(150), TestContext.Current.CancellationToken);
    }

    private async Task<ChannelLocked> ActiveLockAsync(FakeChannel channel)
        => (await this.Settings.GetActiveChannelLockAsync(new ChannelId(channel.Id), new GuildId(this.Guild.Id))).ShouldSucceed();
}
