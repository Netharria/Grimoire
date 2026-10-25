// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text.Json.Nodes;
using Grimoire.Features.Leveling;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Domain.Values;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using Grimoire.Tests.Integration.FakeDiscord;
using Microsoft.Extensions.DependencyInjection;
using LeaderboardOption = Grimoire.Features.Leveling.UserCommands.GetLeaderboard.LeaderboardOption;

namespace Grimoire.Tests.Integration.Leveling;

/// <summary>
///     <c>/Level</c>, <c>/Leaderboard</c> and <c>LeaderboardRefreshBackgroundTask</c> (backlog A-26). XP is seeded
///     directly. The leaderboard reads a materialized view, so tests refresh it after seeding and after the reset,
///     because Respawn doesn't. With the default settings level 2 starts at 15 XP and level 3 at 23.
/// </summary>
[Collection("Test collection")]
public sealed class LevelAndLeaderboardTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private readonly GrimoireCoreFactory _database = database;
    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;
    private SettingsModule Settings => this._bot.Services.GetRequiredService<SettingsModule>();
    private GuildId GuildId => new(this.Guild.Id);
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await this._database.ResetDatabase();
        await this.RefreshLeaderboardAsync();
        this._bot = await GrimoireTestBot.StartAsync(this._database);
        await this._bot.SetModuleAsync(Module.Leveling, true);
    }

    public async ValueTask DisposeAsync()
    {
        await this._bot.DisposeAsync();
        await this._database.ResetDatabase();
        await this.RefreshLeaderboardAsync();
    }

    // ── /Level ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Level_WithNoXp_ShowsLevelOne()
    {
        var embed = (await this._bot.RunSlashCommandAndWaitAsync("level", this.Guild.User1)).Embed!;

        embed["title"]!.GetValue<string>().ShouldStartWith("Level and EXP for ");
        Field(embed, "XP").ShouldBe("0");
        Field(embed, "Level").ShouldBe("1");
        Field(embed, "Progress").ShouldBe("0/15");
        Field(embed, "Next Reward").ShouldBe("None");
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task Level_ShowsTheMembersXpAndProgress()
    {
        await this.SeedXpAsync((this.Guild.User1, 20));

        var embed = (await this._bot.RunSlashCommandAndWaitAsync("level", this.Guild.User1)).Embed!;

        Field(embed, "XP").ShouldBe("20");
        Field(embed, "Level").ShouldBe("2");
        Field(embed, "Progress").ShouldBe("5/8");
    }

    [Fact]
    public async Task Level_ForAnotherUser_ShowsTheirXp()
    {
        await this.SeedXpAsync((this.Guild.Moderator, 7));

        var embed = (await this._bot.RunSlashCommandAndWaitAsync("level", this.Guild.User1,
            SlashOption.User("user", this.Guild.Moderator))).Embed!;

        Field(embed, "XP").ShouldBe("7");
    }

    /// <summary>
    ///     The reply is ephemeral for members without Manage Messages outside the user command channel, and public for
    ///     moderators or in that channel.
    /// </summary>
    [Theory]
    [InlineData("member", false, true)]
    [InlineData("moderator", false, false)]
    [InlineData("member", true, false)]
    public async Task Level_IsEphemeral_UnlessAModeratorOrInTheUserCommandChannel(string who, bool inCommandChannel, bool ephemeral)
    {
        if (inCommandChannel)
            (await this.Settings.SetUserCommandChannelSetting(this.GuildId, new ModeratorId(this.Guild.Owner.Id),
                new ChannelId(this.Guild.General.Id))).ShouldBeSetOrUnchanged();

        var reply = await this._bot.RunSlashCommandAndWaitAsync(this.Guild.General, "level",
            who == "moderator" ? this.Guild.Moderator : this.Guild.User1);

        reply.IsEphemeral.ShouldBe(ephemeral);
    }

    /// <summary>The next reward shows its role and the level it's given at.</summary>
    [Fact(Skip = "Fails: Next Reward prints the XP needed for the next level after \"at level\", not the reward's " +
                 "level. KI-42")]
    public async Task Level_ShowsTheNextRewardAndItsLevel()
    {
        await this.AddRewardAsync(this.Guild.KickerRoleId, 4);

        var embed = (await this._bot.RunSlashCommandAndWaitAsync("level", this.Guild.User1)).Embed!;

        Field(embed, "Next Reward").ShouldBe($"<@&{this.Guild.KickerRoleId}>\n at level 4");
    }

    /// <summary>With several rewards ahead, the nearest one is shown.</summary>
    [Fact(Skip = "Fails: Next Reward takes the first reward above the member's level from an unordered set, so it " +
                 "can show a later reward than the nearest. KI-42")]
    public async Task Level_ShowsTheNearestReward()
    {
        await this.AddRewardAsync(this.Guild.BannerRoleId, 9);
        await this.AddRewardAsync(this.Guild.KickerRoleId, 4);
        await this.AddRewardAsync(this.Guild.ManagerRoleId, 6);

        var embed = (await this._bot.RunSlashCommandAndWaitAsync("level", this.Guild.User1)).Embed!;

        Field(embed, "Next Reward").ShouldStartWith($"<@&{this.Guild.KickerRoleId}>");
    }

    [Theory]
    [InlineData("level")]
    [InlineData("leaderboard")]
    public async Task LevelingCommands_NeedTheModule(string command)
    {
        await this._bot.SetModuleAsync(Module.Leveling, false);

        (await this._bot.RunSlashCommandAndWaitAsync(command, this.Guild.User1, command == "leaderboard"
                ? [SlashOption.Enum("option", LeaderboardOption.Top)]
                : []))
            .Description.ShouldBe("This module is disabled in this server.");
    }

    // ── /Leaderboard ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Leaderboard_Top_RanksMembersByXp()
    {
        await this.SeedXpAsync((this.Guild.User1, 10), (this.Guild.Moderator, 30), (this.Guild.Admin, 20), (this.Guild.User1, 5));

        var reply = await this.LeaderboardAsync(LeaderboardOption.Top);

        reply.Embed!["author"]!["name"]!.GetValue<string>().ShouldBe("LeaderBoard");
        reply.Description.ShouldBe(string.Join('\n',
            $"**1** {this.Guild.Moderator.Mention} **XP:** 30",
            $"**2** {this.Guild.Admin.Mention} **XP:** 20",
            $"**3** {this.Guild.User1.Mention} **XP:** 15"));
        reply.Embed!["footer"]!["text"]!.GetValue<string>().ShouldBe("Total Users 3");
    }

    /// <summary>The user view shows five places above the member and nine below.</summary>
    [Fact]
    public async Task Leaderboard_User_ShowsTheMembersNeighbourhood()
    {
        var others = Enumerable.Range(1, 20).Select(i => new FakeUser(1_100_000_000_000_007_000 + (ulong)i, $"u{i}")).ToArray();
        await this.SeedXpAsync([.. others.Select((user, i) => (user, 1000 - i * 10)), (this.Guild.User1, 905)]);

        var lines = (await this.LeaderboardAsync(LeaderboardOption.User, this.Guild.User1)).Description!.Split('\n');

        lines.Length.ShouldBe(15);
        lines[0].ShouldStartWith("**6** ");
        lines[5].ShouldBe($"**11** {this.Guild.User1.Mention} **XP:** 905");
        lines[^1].ShouldStartWith("**20** ");
    }

    [Fact]
    public async Task Leaderboard_Me_WithNoXp_SaysNotFound()
        => (await this.LeaderboardAsync(LeaderboardOption.Me)).Description.ShouldBe("User not found on the leaderboard");

    [Fact]
    public async Task Leaderboard_UserOptionWithoutAUser_IsRejected()
        => (await this.LeaderboardAsync(LeaderboardOption.User)).Description
            .ShouldBe("You must specify a user when selecting the 'User' option.");

    /// <summary>New XP shows on the leaderboard only after the refresh task runs.</summary>
    [Fact]
    public async Task Leaderboard_PicksUpNewXp_AfterTheRefreshTask()
    {
        await this.SeedXpAsync((this.Guild.User1, 10));
        await this.SeedXpAsync(false, (this.Guild.Moderator, 50));

        (await this.LeaderboardAsync(LeaderboardOption.Top)).Description.ShouldBe($"**1** {this.Guild.User1.Mention} **XP:** 10");

        await this._bot.RunBackgroundTickAsync<LeaderboardRefreshBackgroundTask>();

        (await this.LeaderboardAsync(LeaderboardOption.Top)).Description!.Split('\n')[0]
            .ShouldBe($"**1** {this.Guild.Moderator.Mention} **XP:** 50");
    }

    private Task<FakeReply> LeaderboardAsync(LeaderboardOption option, FakeUser? user = null)
        => this._bot.RunSlashCommandAndWaitAsync("leaderboard", this.Guild.User1,
        [
            SlashOption.Enum("option", option),
            .. user is null ? Array.Empty<SlashOption>() : [SlashOption.User("user", user)]
        ]);

    private Task SeedXpAsync(params (FakeUser User, int Xp)[] entries) => this.SeedXpAsync(true, entries);

    private async Task SeedXpAsync(bool refresh, params (FakeUser User, int Xp)[] entries)
    {
        await using (var seed = this._database.CreateDbContext())
        {
            seed.XpHistory.AddRange(entries.Select(entry => EarnedXp.Create(PositiveXpAmount.Create(entry.Xp).ShouldSucceed(),
                new UserId(entry.User.Id), this.GuildId, DateTimeOffset.UtcNow.AddMinutes(-10)).ShouldSucceed()));
            await seed.SaveChangesAsync(Token);
        }

        if (refresh)
            await this.RefreshLeaderboardAsync();
    }

    private async Task RefreshLeaderboardAsync()
    {
        await using var dbContext = this._database.CreateDbContext();
        await dbContext.Database.ExecuteSqlRawAsync("REFRESH MATERIALIZED VIEW leaderboard_view", Token);
    }

    private async Task AddRewardAsync(ulong roleId, int level)
        => (await this.Settings.SetRewardAsync(RewardAdded.Create(new RoleId(roleId), this.GuildId, level, null,
            new ModeratorId(this.Guild.Owner.Id), DateTimeOffset.UtcNow).ShouldSucceed())).ShouldSucceed();

    private static string Field(JsonNode embed, string name)
        => embed["fields"]!.AsArray().Single(field => field!["name"]!.GetValue<string>() == name)!["value"]!.GetValue<string>();
}
