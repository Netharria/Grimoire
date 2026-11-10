// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text.Json.Nodes;
using Grimoire.Settings.Services;
using Grimoire.Tests.Integration.FakeDiscord;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Tests.Integration.Moderation;

/// <summary>
///     <c>/Diagnostics Check</c> gates and what the report says on a real guild cache, cases DIAG-02 to DIAG-41 in
///     <c>docs/testing/04-spamfilter-diagnostics.md</c>, and the report that follows <c>/Mutes Create</c> and
///     <c>/Mutes Refresh</c>, DIAG-81 to DIAG-84. The analysis on plain snapshots is unit tested in
///     <c>Features/Moderation/Diagnostics/</c>; these tests read the attached report, so the snapshot builder and the
///     cache are covered too.
/// </summary>
/// <remarks>
///     Most tests start from a "muted guild": @everyone may speak, and a <c>Muted</c> role denies the text set on text,
///     forum and media channels and the voice set on voice and stage channels, as <c>/Mutes Create</c> leaves it. So
///     the report has no findings until a test adds one.
/// </remarks>
[Collection("Test collection")]
public sealed class DiagnosticsFindingsTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    internal const string ReportFile = "moderation-diagnostics.txt";
    internal const string MutesSection = "Mutes may not work in these channels";
    internal const string LocksSection = "Locks may not work in these channels";

    private const string MuteRoleNotDenied =
        "The mute role does not deny the needed permissions here, so muted members can still use this channel. Fix: run /Mutes Refresh.";

    private const ulong SendMessages = 1UL << 11;
    private const ulong SendTtsMessages = 1UL << 12;
    private const ulong AddReactions = 1UL << 6;
    private const ulong SendMessagesInThreads = 1UL << 38;
    private const ulong Connect = 1UL << 20;
    private const ulong Speak = 1UL << 21;
    private const ulong TextLock = SendMessages | SendMessagesInThreads | SendTtsMessages | AddReactions;
    private const ulong VoiceLock = Connect | Speak;

    /// <summary>What @everyone holds in a usual server: it can see, type and talk.</summary>
    private const ulong EveryoneCanSpeak = FakeGuild.ViewChannelPermission | TextLock | VoiceLock;

    /// <summary>A bot without Administrator, Manage Roles or Manage Channels.</summary>
    private const ulong PlainBot = FakeGuild.ViewChannelPermission | FakeGuild.SendMessagesPermission | FakeGuild.EmbedLinksPermission;

    private readonly GrimoireCoreFactory _database = database;
    private GrimoireTestBot _bot = null!;
    private ulong _muteRoleId;

    private FakeGuild Guild => this._bot.Guild;
    private SettingsModule Settings => this._bot.Services.GetRequiredService<SettingsModule>();

    public async ValueTask InitializeAsync() => await this._database.ResetDatabase();

    public async ValueTask DisposeAsync()
    {
        if (this._bot is not null)
            await this._bot.DisposeAsync();
    }

    // ── D. Check: gates ──────────────────────────────────────────────────────

    /// <summary>DIAG-02: the owner and a member whose only power is Manage Guild may run it.</summary>
    [Fact]
    public async Task Check_IsAllowedForTheOwner_AndAManageGuildMember()
    {
        await this.StartMutedGuildAsync();
        this.Guild.SetRoles(this.Guild.User1, this.Guild.ManagerRoleId);

        foreach (var member in new[] { this.Guild.Owner, this.Guild.User1 })
        {
            var (reply, report) = await this.CheckAsync(member);
            reply.Embed!["title"]!.GetValue<string>().ShouldBe("Moderation diagnostics", member.Username);
            report.ShouldStartWith("Moderation diagnostics");
        }
    }

    /// <summary>DIAG-05: Discord never offers it in a DM, so <c>RequireGuild</c> is never reached from one.</summary>
    [Fact]
    public async Task Diagnostics_IsNotOfferedInDirectMessages()
    {
        await this.StartMutedGuildAsync();

        this._bot.Rest.RegisteredCommands["diagnostics"]["dm_permission"]?.GetValue<bool>().ShouldBe(false);
    }

    /// <summary>
    ///     DIAG-06, DIAG-07, DIAG-24 and DIAG-25: a bot without Manage Roles or Manage Channels still answers, even
    ///     from a channel it can't send in. Both permissions are reported, and the mute role sitting above the bot
    ///     is not, since Manage Roles comes first.
    /// </summary>
    [Fact]
    public async Task Check_WithoutManageRolesOrManageChannels_RunsAnywhere_AndReportsBothAlone()
    {
        await this.StartMutedGuildAsync(guild => guild.BotPermissions = PlainBot, muteRolePosition: FakeGuild.RoleAboveTheBot);

        var (reply, _) = await this.CheckAsync(this.Guild.Admin, this.Guild.Private);

        reply.IsEphemeral.ShouldBeTrue();
        reply.Description!.Split('\n').Where(line => line.StartsWith("- ", StringComparison.Ordinal)).ShouldBe([
            "- The bot does not have the Manage Roles permission, so it cannot mute or unmute anyone. **Fix:** give the bot's role Manage Roles.",
            "- The bot does not have the Manage Channels permission, so it cannot lock channels. **Fix:** give the bot's role Manage Channels."
        ]);
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>DIAG-08: each guild's report covers its own mute role, ignore list and channels.</summary>
    [Fact]
    public async Task Check_InEachGuild_ReportsOnlyThatGuild()
    {
        await this.StartMutedGuildAsync(secondGuild: true);
        var second = this._bot.SecondGuild!;
        await this._bot.ConfigureModerationAsync(second);
        await this._bot.RunSlashCommandAndWaitAsync("diagnostics", this.Guild.Admin, SlashOption.Group("ignore",
            SlashOption.Subcommand("add", SlashOption.MentionableRole("items_0", this.Guild.ModeratorRoleId))));

        var (_, first) = await this.CheckAsync(this.Guild.Admin);
        var (_, other) = await this.CheckAsync(this.Guild.Admin, second.General);

        first.ShouldContain("Ignored roles (excluded on request): Moderator");
        first.ShouldNotContain("No mute role is configured.");
        other.ShouldContain("No mute role is configured.");
        other.ShouldNotContain("Ignored roles");
    }

    /// <summary>DIAG-41: Check reads the cache, so an overwrite changed by hand shows once Discord announces it.</summary>
    [Fact]
    public async Task Check_AfterAnOverwriteIsChangedByHand_ReflectsTheChange()
    {
        await this.StartMutedGuildAsync();
        (await this.CheckAsync(this.Guild.Admin)).Report.ShouldNotContain("Moderator");

        this.Guild.SetOverwrite(this.Guild.General.Id, this.Guild.ModeratorRoleId, 0, SendMessages, 0);
        this._bot.Gateway.Dispatch("CHANNEL_UPDATE", this.Guild.ChannelJson(this.Guild.General));

        var report = await GrimoireTestBot.EventuallyAsync(async () => (await this.CheckAsync(this.Guild.Admin)).Report,
            text => Findings(text, MutesSection, this.Guild.General).Count > 0);
        Findings(report, MutesSection, this.Guild.General).ShouldBe([
            "Members with these roles can still use this channel while muted: Moderator. Fix: remove the explicit allow "
            + "for those roles in this channel, or ignore the roles if that is intended."
        ]);
    }

    // ── E. What the report says ──────────────────────────────────────────────

    /// <summary>DIAG-35: announcement channels, categories and threads are never checked, so none is listed.</summary>
    [Fact]
    public async Task Check_SkipsAnnouncementChannels_Categories_AndThreads()
    {
        await this.StartMutedGuildAsync();

        var (reply, report) = await this.CheckAsync(this.Guild.Admin);

        report.ShouldEndWith("No problems found.\n");
        foreach (var skipped in new[] { this.Guild.Announcements, this.Guild.Category, this.Guild.PublicThread, this.Guild.PrivateThread, this.Guild.ForumPost })
            report.Contains($"#{skipped.Name}", StringComparison.Ordinal).ShouldBeFalse(skipped.Name);
        reply.Description.ShouldEndWith("No problems found.");
        this._bot.Rest.Unhandled.ShouldBeEmpty();
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>DIAG-36: forum and media channels use the text set, so both are listed when the mute role isn't denied.</summary>
    [Fact]
    public async Task Check_ListsForumAndMediaChannels_WithoutAMuteOverwrite()
    {
        await this.StartMutedGuildAsync(guild => guild.HasMediaChannel = true, unmuted: g => [g.Forum, g.Media]);

        var report = (await this.CheckAsync(this.Guild.Admin)).Report;

        Findings(report, MutesSection, this.Guild.Forum).ShouldBe([MuteRoleNotDenied]);
        Findings(report, MutesSection, this.Guild.Media).ShouldBe([MuteRoleNotDenied]);
    }

    /// <summary>DIAG-36 (KI-59): <c>/Lock</c> refuses media channels, so they should not get lock findings. Forums do.</summary>
    [Fact(Skip = "Fails: media channels are lock-checked like text channels, though /Lock refuses them. KI-59")]
    public async Task Check_DoesNotLockCheckMediaChannels()
    {
        await this.StartMutedGuildAsync(guild =>
        {
            guild.HasMediaChannel = true;
            guild.SetOverwrite(guild.Forum.Id, guild.ModeratorRoleId, 0, SendMessages, 0);
            guild.SetOverwrite(guild.Media.Id, guild.ModeratorRoleId, 0, SendMessages, 0);
        });

        var report = (await this.CheckAsync(this.Guild.Admin)).Report;

        Findings(report, LocksSection, this.Guild.Forum).ShouldNotBeEmpty("forums are lock-checked");
        Findings(report, LocksSection, this.Guild.Media).ShouldBeEmpty();
    }

    /// <summary>
    ///     DIAG-37 (KI-58): the bot's own role often has an allow overwrite, such as Send Messages in the mod log. It
    ///     should be left out like Administrator roles, not reported as speaking while muted or locked.
    /// </summary>
    [Fact(Skip = "Fails: the bot's own role is reported as able to speak while muted and while locked. KI-58")]
    public async Task Check_LeavesOutTheBotsOwnRole()
    {
        await this.StartMutedGuildAsync(guild =>
        {
            guild.BotPermissions = PlainBot | FakeGuild.ManageRolesPermission | FakeGuild.ManageChannelsPermission;
            guild.SetOverwrite(guild.ModLog.Id, guild.BotRoleId, 0, SendMessages, 0);
        });

        var report = (await this.CheckAsync(this.Guild.Admin)).Report;

        Findings(report, MutesSection, this.Guild.ModLog).ShouldBeEmpty();
        Findings(report, LocksSection, this.Guild.ModLog).ShouldBeEmpty();
    }

    /// <summary>
    ///     DIAG-38 (KI-57): a channel @everyone can't see needs no mute overwrite, because no muted member can reach
    ///     it. View Channel isn't considered, so it is reported.
    /// </summary>
    [Fact(Skip = "Fails: View Channel is ignored, so a channel hidden from @everyone is listed under mutes. KI-57")]
    public async Task Check_LeavesOutAChannelThatEveryoneCannotSee()
    {
        await this.StartMutedGuildAsync(
            guild => guild.SetOverwrite(guild.Private.Id, guild.Id, 0, 0, FakeGuild.ViewChannelPermission),
            unmuted: g => [g.Private]);

        var report = (await this.CheckAsync(this.Guild.Admin)).Report;

        report.ShouldNotContain($"#{this.Guild.Private.Name}");
    }

    /// <summary>
    ///     DIAG-39 (KI-56): voice and stage channels have a text chat, so the mute role must deny Send Messages there
    ///     too. <c>/Mutes Create</c> denies only Connect and Speak.
    /// </summary>
    [Fact(Skip = "Fails: the mute overwrite on voice and stage channels denies only Connect and Speak. KI-56")]
    public async Task MutesCreate_DeniesSendMessagesInVoiceAndStageChat()
    {
        await this.StartMutedGuildAsync(configureMuteRole: false);
        var interaction = this._bot.RunSlashCommand("mutes", this.Guild.Admin, SlashOption.Subcommand("create"));
        await this._bot.Rest.WaitForRequestAsync(interaction.IsFollowup, TimeSpan.FromSeconds(15));
        var roleId = (await this.Settings.GetConfiguredMuteRole(new GuildId(this.Guild.Id), cancellationToken: TestContext.Current.CancellationToken)).ShouldSucceed()!.Value.Value;

        foreach (var channel in new[] { this.Guild.Voice, this.Guild.Stage })
            ((this.Guild.OverwriteOf(channel.Id, roleId)?.Deny ?? 0) & SendMessages).ShouldBe(SendMessages, channel.Name);
    }

    /// <summary>
    ///     DIAG-39 (KI-56): the diagnostics use the same voice set, so a voice channel whose mute overwrite denies only
    ///     Connect and Speak isn't flagged, though muted members can still type in its chat.
    /// </summary>
    [Fact(Skip = "Fails: the diagnostics check voice channels for Connect and Speak only, not their text chat. KI-56")]
    public async Task Check_FlagsAVoiceChannelWhoseChatMutedMembersCanUse()
    {
        await this.StartMutedGuildAsync();

        var report = (await this.CheckAsync(this.Guild.Admin)).Report;

        Findings(report, MutesSection, this.Guild.Voice).ShouldNotBeEmpty();
        Findings(report, MutesSection, this.Guild.Stage).ShouldNotBeEmpty();
    }

    // ── G. The follow-up after /Mutes ────────────────────────────────────────

    /// <summary>DIAG-81: with no allow overwrites, the follow-up uses the new role and finds nothing wrong.</summary>
    [Fact]
    public async Task MutesCreate_FollowUp_UsesTheNewRole_AndFindsNoProblems()
    {
        await this.StartMutedGuildAsync(configureMuteRole: false);

        var (embed, report) = await this.MutesFollowUpAsync("create");

        report.ShouldNotContain("No mute role");
        report.ShouldBe("Moderation diagnostics\n======================\n\n"
                        + "Administrator roles (excluded, Administrator ignores channel permissions): Grimoire, Admin\n\n"
                        + "No problems found.\n");
        embed["description"]!.GetValue<string>().ShouldEndWith("\n\nNo problems found.");
        this._bot.Rest.Unhandled.ShouldBeEmpty();
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>DIAG-82 and DIAG-83: Refresh's follow-up reports a role allowed to speak, unless it is ignored.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MutesRefresh_FollowUp_ReportsAnAllowedRole_UnlessIgnored(bool ignored)
    {
        await this.StartMutedGuildAsync(guild =>
            guild.SetOverwrite(guild.General.Id, guild.ModeratorRoleId, 0, SendMessages, 0));
        if (ignored)
            await this._bot.RunSlashCommandAndWaitAsync("diagnostics", this.Guild.Admin, SlashOption.Group("ignore",
                SlashOption.Subcommand("add", SlashOption.MentionableRole("items_0", this.Guild.ModeratorRoleId))));

        var (_, report) = await this.MutesFollowUpAsync("refresh");

        const string fix = "Fix: remove the explicit allow for those roles in this channel, or ignore the roles if that is intended.";
        if (ignored)
        {
            report.ShouldContain("Ignored roles (excluded on request): Moderator\n");
            Findings(report, MutesSection, this.Guild.General).ShouldBeEmpty();
            Findings(report, LocksSection, this.Guild.General).ShouldBeEmpty();
        }
        else
        {
            Findings(report, MutesSection, this.Guild.General)
                .ShouldBe([$"Members with these roles can still use this channel while muted: Moderator. {fix}"]);
            Findings(report, LocksSection, this.Guild.General)
                .ShouldBe([$"Members with these roles can still use this channel while it is locked: Moderator. {fix}"]);
        }
    }

    /// <summary>DIAG-84: a channel Create couldn't set is named in the reply, then listed in the follow-up.</summary>
    [Fact]
    public async Task MutesCreate_FollowUp_ListsAChannelItCouldNotSet()
    {
        await this.StartMutedGuildAsync(guild => guild.BotPermissions = PlainBot | FakeGuild.ManageRolesPermission,
            configureMuteRole: false);

        var interaction = this._bot.RunSlashCommand("mutes", this.Guild.Admin, SlashOption.Subcommand("create"));
        var report = (await this._bot.Rest.WaitForRequestAsync(interaction.IsFollowup, TimeSpan.FromSeconds(15))).Files[ReportFile];

        this._bot.Rest.Requests.Where(interaction.IsOriginalResponseEdit).Last().Json!["embeds"]![0]!["description"]!
            .GetValue<string>().ShouldEndWith($"was not able to set permissions for the following channels. {this.Guild.Private.Mention}");
        Findings(report, MutesSection, this.Guild.Private).ShouldBe([MuteRoleNotDenied]);
        Findings(report, MutesSection, this.Guild.General).ShouldBeEmpty();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    ///     Starts the bot on a muted guild (see the class remarks). Channels <paramref name="unmuted" /> picks get no
    ///     mute overwrite. With <paramref name="configureMuteRole" /> false the role and overwrites are left out, for
    ///     <c>/Mutes Create</c> to make.
    /// </summary>
    private async Task StartMutedGuildAsync(Action<FakeGuild>? configureGuild = null,
        Func<FakeGuild, FakeChannel[]>? unmuted = null, bool configureMuteRole = true, int muteRolePosition = 1,
        bool secondGuild = false)
    {
        this._bot = await GrimoireTestBot.StartAsync(this._database, guild =>
        {
            guild.EveryonePermissions = EveryoneCanSpeak;
            configureGuild?.Invoke(guild);
            if (!configureMuteRole)
                return;
            this._muteRoleId = guild.AddRole("Muted", position: muteRolePosition);
            var skipped = unmuted?.Invoke(guild) ?? [];
            foreach (var channel in guild.Channels.Where(channel => !skipped.Contains(channel)))
                if (channel.Type is FakeChannel.TextType or FakeChannel.ForumType or FakeChannel.MediaType)
                    guild.SetOverwrite(channel.Id, this._muteRoleId, 0, 0, TextLock);
                else if (channel.Type is FakeChannel.VoiceType or FakeChannel.StageType)
                    guild.SetOverwrite(channel.Id, this._muteRoleId, 0, 0, VoiceLock);
        }, secondGuild: secondGuild);
        await this._bot.ConfigureModerationAsync();
        if (configureMuteRole)
            (await this.Settings.SetMuteRole(new GuildId(this.Guild.Id), new ModeratorId(this.Guild.Owner.Id),
                new RoleId(this._muteRoleId))).ShouldSucceed();
    }

    private async Task<(FakeReply Reply, string Report)> CheckAsync(FakeUser invoker, FakeChannel? channel = null)
    {
        var interaction = this._bot.RunSlashCommand(channel ?? this.Guild.General, "diagnostics", invoker,
            SlashOption.Subcommand("check"));
        var reply = await FakeReply.WaitForAsync(this._bot.Rest, interaction);
        return (reply, this._bot.Rest.Requests.Last(interaction.IsOriginalResponseEdit).Files[ReportFile]);
    }

    private async Task<(JsonNode Embed, string Report)> MutesFollowUpAsync(string subcommand)
    {
        var interaction = this._bot.RunSlashCommand("mutes", this.Guild.Admin, SlashOption.Subcommand(subcommand));
        var followUp = await this._bot.Rest.WaitForRequestAsync(interaction.IsFollowup, TimeSpan.FromSeconds(15));
        return (followUp.Json!["embeds"]![0]!, followUp.Files[ReportFile]);
    }

    /// <summary>The "  - " lines listed for <paramref name="channel" /> in one section of the report file.</summary>
    internal static List<string> Findings(string report, string section, FakeChannel channel)
    {
        List<string> found = [];
        var inSection = false;
        string? current = null;
        foreach (var line in report.Split('\n'))
            if (line == section)
                inSection = true;
            else if (!inSection || line.StartsWith("---", StringComparison.Ordinal))
                continue;
            else if (line.Length == 0)
                break;
            else if (line.StartsWith('#'))
                current = line[1..];
            else if (current == channel.Name && line.StartsWith("  - ", StringComparison.Ordinal))
                found.Add(line[4..]);
        return found;
    }
}
