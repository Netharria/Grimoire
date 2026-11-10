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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Tests.Integration.Shared;

/// <summary>
///     S-4: the mute and XP-ignore lookups now report a failure instead of throwing, so each caller has to handle it.
///     These check that none of them carries on as if it had worked.
/// </summary>
[Collection("Test collection")]
public sealed class SettingFailureCallerTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private static readonly TimeSpan NothingHappensWindow = TimeSpan.FromSeconds(2);

    private GrimoireTestBot _bot = null!;
    private ulong _muteRoleId;

    private FakeGuild Guild => this._bot.Guild;
    private GuildId GuildId => new(this.Guild.Id);
    private SettingsModule Settings => this._bot.Services.GetRequiredService<SettingsModule>();
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await database.ResetDatabase();
        this._bot = await GrimoireTestBot.StartAsync(database);
        await this._bot.ConfigureModerationAsync();
        foreach (var module in new[] { Module.Leveling, Module.AntiSpam })
            await this._bot.SetModuleAsync(module, true);
        this._muteRoleId = this._bot.Rest.CreateRole("Muted");
        (await this.Settings.SetMuteRole(this.GuildId, new ModeratorId(this.Guild.Owner.Id), new RoleId(this._muteRoleId), Token))
            .ShouldSucceed();
        // Read now, so these come from the cache once a table is broken.
        foreach (var module in new[] { Module.Leveling, Module.Moderation, Module.AntiSpam })
            await this.Settings.IsModuleEnabled(module, this.GuildId, Token);
        await this.Settings.GetLevelingSettings(this.GuildId, Token);
        await this.Settings.GetLevelingRewardsAsync(this.GuildId, Token);
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    /// <summary>Nothing would ever lift a mute role given without a record, so the spam filter mustn't give one.</summary>
    [Fact]
    public async Task SpamFilter_WhenTheMuteCannotBeRecorded_GivesNoMuteRole()
    {
        await using (await database.RefuseSettingsInsertsAsync(nameof(Mute)))
        {
            for (var i = 0; i < 6; i++)
            {
                this._bot.SendMessage(this.Guild.User1, this.Guild.General, "buy cheap gold now");
                await Task.Delay(TimeSpan.FromMilliseconds(30), Token);
            }

            (await this._bot.Rest.ReceivesRequestWithinAsync(request => request.Method == HttpMethod.Put
                    && request.Path.EndsWith($"/roles/{this._muteRoleId}", StringComparison.Ordinal), NothingHappensWindow))
                .ShouldBeFalse();
        }
    }

    [Fact]
    public async Task Unmute_WhenTheMuteCannotBeCleared_SaysSo()
    {
        (await this.Settings.AddMute(new UserId(this.Guild.User1.Id), this.GuildId, new ModeratorId(this.Guild.Moderator.Id),
            new SinId(1L), DateTimeOffset.UtcNow.AddHours(1), new RoleId(this._muteRoleId), Token)).ShouldSucceed();
        this._bot.SetRolesByHand(this.Guild.User1, this._muteRoleId);

        FakeReply reply;
        await using (await database.RefuseSettingsInsertsAsync(nameof(Mute)))
            reply = await this._bot.RunSlashCommandAndWaitAsync("unmute", this.Guild.Moderator,
                SlashOption.User("user", this.Guild.User1));

        reply.Description.ShouldBe($"Took the mute role off {this.Guild.User1.MemberMention}, but couldn't clear their mute. "
                                   + "It's cleared when it expires; until then a rejoin brings the role back.");
        reply.IsEphemeral.ShouldBeTrue();
        (await this._bot.Rest.ReceivesRequestWithinAsync(this._bot.IsModLogPost, NothingHappensWindow)).ShouldBeFalse();
    }

    [Fact]
    public async Task IgnoreAdd_WhenItCannotBeSaved_SaysSoPrivately_AndLogsNothing()
    {
        FakeReply reply;
        await using (await database.RefuseSettingsInsertsAsync(nameof(XpTrackedItem)))
            reply = await this._bot.RunSlashCommandAndWaitAsync("ignore", this.Guild.Admin,
                SlashOption.Subcommand("add", SlashOption.MentionableUser("items_0", this.Guild.User1)));

        reply.Description.ShouldBe("Could not save the ignored items.");
        reply.IsEphemeral.ShouldBeTrue();
        (await this._bot.Rest.ReceivesRequestWithinAsync(this._bot.IsModLogPost, NothingHappensWindow)).ShouldBeFalse();
    }

    [Fact]
    public async Task UserInfo_WhenTheIgnoreListCannotBeRead_SaysItDoesNotKnow()
    {
        FakeReply reply;
        await using (await database.MakeSettingsTablesUnreadableAsync(nameof(XpTrackedItem)))
            reply = await this._bot.RunSlashCommandAndWaitAsync("user_info", this.Guild.Admin,
                SlashOption.User("user", this.Guild.User1));

        Field(reply.Embed!, "Can Gain Xp").ShouldBe("Unknown");
    }

    /// <summary>As decided for S-4: with the ignore list unknown, nobody gets XP, rather than everybody.</summary>
    [Fact]
    public async Task GainingXp_WhenTheIgnoreListCannotBeRead_AwardsNothing_AndDoesNotCrash()
    {
        await using (await database.MakeSettingsTablesUnreadableAsync(nameof(XpTrackedItem)))
        {
            this._bot.SendMessage(this.Guild.User1, this.Guild.General, "hello");
            await this._bot.Logs.WaitForAsync(logEvent => logEvent.RenderMessage().StartsWith(
                "Settings module operation failed", StringComparison.Ordinal), TimeSpan.FromSeconds(10));
            await Task.Delay(TimeSpan.FromMilliseconds(500), Token);
        }

        await using var dbContext = database.CreateDbContext();
        (await dbContext.XpHistory.AnyAsync(Token)).ShouldBeFalse();
        this._bot.Logs.Events.ShouldNotContain(logEvent => logEvent.MessageTemplate.Text.StartsWith(
            "Unhandled exception in event handler", StringComparison.Ordinal));
    }

    private static string Field(JsonNode embed, string name)
        => embed["fields"]!.AsArray().Single(field => field!["name"]!.GetValue<string>() == name)!["value"]!.GetValue<string>();
}
