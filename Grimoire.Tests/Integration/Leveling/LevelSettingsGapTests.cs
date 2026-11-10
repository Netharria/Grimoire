// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text.Json.Nodes;
using Grimoire.Enums;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using Grimoire.Tests.Integration.FakeDiscord;
using Microsoft.Extensions.DependencyInjection;
using Option = Grimoire.Features.Leveling.Settings.LevelSettingsCommandGroup.LevelSettingsOptions;

namespace Grimoire.Tests.Integration.Leveling;

/// <summary>
///     <c>/LevelSettings</c> cases from <c>06-leveling.md</c> section E that <see cref="LevelSettingsTests" /> doesn't
///     cover (backlog A-47): a deleted log channel, repeating a value, the new values taking effect, and the LogSet
///     channel checks.
/// </summary>
[Collection("Test collection")]
public sealed class LevelSettingsGapTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private static readonly TimeSpan NothingHappensWindow = TimeSpan.FromSeconds(2);

    private readonly GrimoireCoreFactory _database = database;
    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;
    private SettingsModule Settings => this._bot.Services.GetRequiredService<SettingsModule>();
    private GuildId GuildId => new(this.Guild.Id);
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await this._database.ResetDatabase();
        await this.StartAsync();
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    private async Task StartAsync(Action<FakeGuild>? configureGuild = null)
    {
        if (this._bot is not null)
            await this._bot.DisposeAsync();
        this._bot = await GrimoireTestBot.StartAsync(this._database, configureGuild);
        await this._bot.ConfigureModerationAsync();
        await this._bot.SetModuleAsync(Module.Leveling, true);
    }

    // ── View ──────────────────────────────────────────────────────────────────

    /// <summary>LVL-82: a log channel that was deleted should still show something, not a blank line.</summary>
    [Fact(Skip = "Fails: View shows \"**Log-Channel:** \" blank when the channel was deleted. KI-78")]
    public async Task View_WithADeletedLogChannel_ShowsItsIdOrSaysItIsGone()
    {
        (await this.LogSetAsync(ChannelOption.SelectChannel, this.Guild.Announcements)).Description
            .ShouldBe($"Updated the level log to {this.Guild.Announcements.Mention}");
        this._bot.DeleteChannel(this.Guild.Announcements);
        await Task.Delay(TimeSpan.FromMilliseconds(200), Token);

        var line = (await this.LevelSettingsAsync("view")).Description.ShouldNotBeNull()
            .Split('\n').Single(text => text.StartsWith("**Log-Channel:**", StringComparison.Ordinal));

        line.ShouldNotBe("**Log-Channel:** ");
        line.ShouldNotBe("**Log-Channel:** None");
        line.ShouldContain(this.Guild.Announcements.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    // ── Set ───────────────────────────────────────────────────────────────────

    /// <summary>LVL-88: setting the value it already has still replies and logs again.</summary>
    [Fact]
    public async Task Set_TheSameValueAgain_StillRepliesAndLogs()
    {
        var expectedLog = $"{this.Guild.Admin.MemberMention} updated Amount level setting to 5";
        (await this.SetAsync(Option.Amount, 5)).Description.ShouldBe("Updated Amount level setting to 5");
        await this._bot.WaitForModLogEmbedAsync(e => e["description"]?.GetValue<string>() == expectedLog);

        (await this.SetAsync(Option.Amount, 5)).Description.ShouldBe("Updated Amount level setting to 5");

        await GrimoireTestBot.EventuallyAsync(() => Task.FromResult(this.ModLogPostsWith(expectedLog)), count => count == 2);
        this.ModLogPostsWith(expectedLog).ShouldBe(2);
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>LVL-89: a TextTime of 10 gives a 10-minute timeout, and a second message inside it earns nothing.</summary>
    [Fact]
    public async Task Set_TextTime_GivesThatTimeout()
    {
        await this.SetAsync(Option.TextTime, 10);

        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "first");
        var earned = (await this.EarnedAsync(this.Guild.User1, 1)).ShouldHaveSingleItem();
        earned.TimeOut.ShouldBe(DateTimeOffset.UtcNow.AddMinutes(10), TimeSpan.FromSeconds(30));

        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "second");
        (await this.EarnedAsync(this.Guild.User1, 2, NothingHappensWindow)).Count.ShouldBe(1);
    }

    /// <summary>LVL-90: with Base 30, a member at 20 XP is level 1 with 20/30 progress.</summary>
    [Fact]
    public async Task Set_Base_ChangesTheLevelCurve()
    {
        await this.SetAsync(Option.Base, 30);
        await using (var seed = this._database.CreateDbContext())
        {
            seed.XpHistory.Add(EarnedXp.Create(PositiveXpAmount.Create(20).ShouldSucceed(), new UserId(this.Guild.User1.Id),
                this.GuildId, DateTimeOffset.UtcNow.AddMinutes(-10)));
            await seed.SaveChangesAsync(Token);
        }

        var embed = (await this._bot.RunSlashCommandAndWaitAsync("level", this.Guild.User1)).Embed!;

        Field(embed, "Level").ShouldBe("1");
        Field(embed, "Progress").ShouldBe("20/30");
    }

    // ── LogSet ────────────────────────────────────────────────────────────────

    /// <summary>LVL-92: Current Channel uses the channel the command was run in.</summary>
    [Fact]
    public async Task LogSet_CurrentChannel_UsesTheChannelItRanIn()
    {
        var reply = await this._bot.RunSlashCommandAndWaitAsync(this.Guild.Announcements, "level_settings", this.Guild.Admin,
            SlashOption.Subcommand("log_set", SlashOption.Enum("option", ChannelOption.CurrentChannel)));

        reply.Description.ShouldBe($"Updated the level log to {this.Guild.Announcements.Mention}");
        (await this.LogChannelAsync()).ShouldBe(this.Guild.Announcements.Id);
        await this._bot.WaitForModLogEmbedAsync(e => e["description"]?.GetValue<string>()
            == $"{this.Guild.Admin.MemberMention} updated the level log to {this.Guild.Announcements.Mention}.");
    }

    /// <summary>LVL-93: Off clears the log and says so in the mod log.</summary>
    [Fact]
    public async Task LogSet_Off_IsLogged()
    {
        await this.LogSetAsync(ChannelOption.SelectChannel, this.Guild.General);

        (await this.LogSetAsync(ChannelOption.Off)).Description.ShouldBe("Disabled the level log.");

        (await this.LogChannelAsync()).ShouldBeNull();
        await this._bot.WaitForModLogEmbedAsync(e => e["description"]?.GetValue<string>()
            == $"{this.Guild.Admin.MemberMention} disabled the level log.");
    }

    /// <summary>LVL-94: Select Channel without a channel is refused.</summary>
    [Fact]
    public async Task LogSet_SelectChannel_WithoutAChannel_IsRefused()
    {
        var reply = await this.LogSetAsync(ChannelOption.SelectChannel);

        reply.Description.ShouldBe("Select a channel when using the Select Channel option.");
        reply.IsEphemeral.ShouldBeTrue();
        (await this.LogChannelAsync()).ShouldBeNull();
    }

    /// <summary>LVL-96: a channel where the bot can send but not embed links is refused, since every log is an embed.</summary>
    [Fact]
    public async Task LogSet_WhereTheBotCannotEmbedLinks_IsRefused()
    {
        await this.StartAsync(guild => guild.BotPermissions = FakeGuild.ViewChannelPermission | FakeGuild.SendMessagesPermission);

        (await this.LogSetAsync(ChannelOption.SelectChannel, this.Guild.General)).Description
            .ShouldBe($"{this.Guild.Bot.MemberMention} also needs **Embed Links** in that channel to post these logs.");
        (await this.LogChannelAsync()).ShouldBeNull();
    }

    /// <summary>LVL-97: a category or a forum can't hold messages.</summary>
    [Theory]
    [InlineData("category")]
    [InlineData("forum")]
    public async Task LogSet_ACategoryOrForum_IsRefused(string kind)
    {
        var channel = kind == "category" ? this.Guild.Category : this.Guild.Forum;

        (await this.LogSetAsync(ChannelOption.SelectChannel, channel)).Description
            .ShouldBe($"{channel.Mention} can't hold messages. Pick a text channel.");
        (await this.LogChannelAsync()).ShouldBeNull();
    }

    private int ModLogPostsWith(string description)
        => this._bot.Rest.Requests.Where(this._bot.IsModLogPost)
            .Count(request => request.Json?["embeds"]?[0]?["description"]?.GetValue<string>() == description);

    private Task<FakeReply> LevelSettingsAsync(string subcommand, params SlashOption[] options)
        => this._bot.RunSlashCommandAndWaitAsync("level_settings", this.Guild.Admin, SlashOption.Subcommand(subcommand, options));

    private Task<FakeReply> SetAsync(Option option, int value)
        => this.LevelSettingsAsync("set", SlashOption.Enum("setting", option), SlashOption.Integer("value", value));

    private Task<FakeReply> LogSetAsync(ChannelOption option, FakeChannel? channel = null)
        => this.LevelSettingsAsync("log_set",
        [
            SlashOption.Enum("option", option),
            .. channel is null ? Array.Empty<SlashOption>() : [SlashOption.Channel("channel", channel)]
        ]);

    private async Task<ulong?> LogChannelAsync()
        => (await this.Settings.GetLogChannelSetting(GuildLogType.Leveling, this.GuildId)).ShouldSucceed()?.Value;

    private Task<List<EarnedXp>> EarnedAsync(FakeUser user, int count, TimeSpan? window = null)
        => GrimoireTestBot.EventuallyAsync(async () =>
        {
            await using var dbContext = this._database.CreateDbContext();
            return await dbContext.XpHistory.OfType<EarnedXp>()
                .Where(xp => xp.UserId == new UserId(user.Id) && xp.GuildId == this.GuildId).ToListAsync(Token);
        }, earned => earned.Count >= count, window);

    private static string Field(JsonNode embed, string name)
        => embed["fields"]!.AsArray().Single(field => field!["name"]!.GetValue<string>() == name)!["value"]!.GetValue<string>();
}
