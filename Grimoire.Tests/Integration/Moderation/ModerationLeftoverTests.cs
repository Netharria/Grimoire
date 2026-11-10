// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Net;
using Grimoire.Settings.Services;
using Grimoire.Tests.Integration.FakeDiscord;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Tests.Integration.Moderation;

/// <summary>
///     Area 02 cases left over after A-06 and A-08 (backlog A-39): a ban of a member whose DMs are closed (MOD-11), and
///     the diagnostics report that follows <c>/Mutes Refresh</c> (MOD-87).
/// </summary>
[Collection("Test collection")]
public sealed class ModerationLeftoverTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private const string ReportFile = "moderation-diagnostics.txt";

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

    /// <summary>
    ///     MOD-11: a member with closed DMs is still banned and the reply shown. The failed DM is told to the mod log,
    ///     whatever Discord's error (the area doc predates this and expects only a debug log).
    /// </summary>
    [Fact]
    public async Task Ban_OfAMemberWithClosedDms_StillBans_AndTellsTheModLog()
    {
        this._bot.Rest.On(HttpMethod.Post, $"channels/{this.Guild.User1.Id + 1_000}/messages",
            _ => FakeDiscordRest.Error(HttpStatusCode.Forbidden, 50007, "Cannot send messages to this user"));

        var reply = await this._bot.RunSlashCommandAndWaitAsync("ban", this.Guild.Admin, SlashOption.User("user", this.Guild.User1));

        reply.Embed!["author"]!["name"]!.GetValue<string>().ShouldBe("Banned");
        this._bot.Rest.Banned.ShouldContain(this.Guild.User1.Id);
        await this._bot.WaitForModLogEmbedAsync(embed => embed["description"]?.GetValue<string>()
            == $"Was not able to send a direct message with the ban details to {this.Guild.User1.MemberMention}");
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>
    ///     MOD-87: a role that allows Send Messages in a channel would let muted members talk there, and the report
    ///     that follows /Mutes Refresh flags that channel.
    /// </summary>
    [Fact]
    public async Task MutesRefresh_FollowUp_FlagsAChannelWhereARoleCanStillSpeak()
    {
        var roleId = this._bot.Rest.CreateRole("Muted");
        (await this.Settings.SetMuteRole(new GuildId(this.Guild.Id), new ModeratorId(this.Guild.Owner.Id), new RoleId(roleId),
            TestContext.Current.CancellationToken)).ShouldSucceed();
        this.Guild.SetOverwrite(this.Guild.General.Id, this.Guild.HigherRoleId, 0, FakeGuild.SendMessagesPermission, 0);
        this._bot.Gateway.Dispatch("CHANNEL_UPDATE", this.Guild.ChannelJson(this.Guild.General));
        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);

        var interaction = this._bot.RunSlashCommand("mutes", this.Guild.Admin, SlashOption.Subcommand("refresh"));
        var followUp = await this._bot.Rest.WaitForRequestAsync(request => request.Method == HttpMethod.Post
                                                                           && request.Path.EndsWith($"/{interaction.Token}", StringComparison.Ordinal),
            TimeSpan.FromSeconds(15));

        followUp.Json!["embeds"]![0]!["title"]!.GetValue<string>().ShouldBe("Moderation diagnostics");
        var report = followUp.Files[ReportFile];
        report.ShouldContain($"#{this.Guild.General.Name}\n  - Members with these roles can still use this channel while muted: Higher.");
    }
}
