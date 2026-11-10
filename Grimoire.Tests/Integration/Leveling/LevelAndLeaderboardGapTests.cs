// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text.Json.Nodes;
using DSharpPlus.Entities;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using Grimoire.Tests.Integration.FakeDiscord;
using Microsoft.Extensions.DependencyInjection;
using LeaderboardOption = Grimoire.Features.Leveling.UserCommands.GetLeaderboard.LeaderboardOption;
using XpOption = Grimoire.Features.Leveling.Awards.ReclaimUserXp.XpOption;

namespace Grimoire.Tests.Integration.Leveling;

/// <summary>
///     <c>/Level</c> and <c>/Leaderboard</c> cases from <c>06-leveling.md</c> sections C and D that
///     <see cref="LevelAndLeaderboardTests" /> doesn't cover (backlog A-46). The leaderboard reads a materialized view,
///     so the view is refreshed after seeding and around the reset, because Respawn doesn't.
/// </summary>
[Collection("Test collection")]
public sealed class LevelAndLeaderboardGapTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;
    private SettingsModule Settings => this._bot.Services.GetRequiredService<SettingsModule>();
    private GuildId GuildId => new(this.Guild.Id);
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await database.ResetDatabase();
        await this.RefreshLeaderboardAsync();
        this._bot = await GrimoireTestBot.StartAsync(database);
        await this._bot.SetModuleAsync(Module.Leveling, true);
    }

    public async ValueTask DisposeAsync()
    {
        await this._bot.DisposeAsync();
        await database.ResetDatabase();
        await this.RefreshLeaderboardAsync();
    }

    /// <summary>
    ///     LVL-56: <c>/Level</c> for a user who left still shows their XP, under their username, with their user
    ///     avatar (the default one here) and blurple, since they have no banner color.
    /// </summary>
    [Fact]
    public async Task Level_ForAUserWhoLeft_ShowsTheirXp_WithTheirUserProfile()
    {
        await this.SeedXpAsync((this.Guild.User1, 20));
        this._bot.MemberLeaves(this.Guild.User1);

        var embed = (await this._bot.RunSlashCommandAndWaitAsync("level", this.Guild.Admin,
            SlashOption.User("user", this.Guild.User1))).Embed!;

        embed["title"]!.GetValue<string>().ShouldBe($"Level and EXP for {this.Guild.User1.Username}");
        Field(embed, "XP").ShouldBe("20");
        Field(embed, "Level").ShouldBe("2");
        embed["color"]!.GetValue<int>().ShouldBe(DiscordColor.Blurple.Value);
        embed["thumbnail"]!["url"]!.GetValue<string>().ShouldContain("/embed/avatars/");
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>
    ///     LVL-57: when the nearest reward's role was deleted and no XP gain has removed that reward yet, Next Reward
    ///     should show the next reward that still has a role.
    /// </summary>
    [Fact(Skip = "Fails: the nearest reward is picked before its role is looked up, so a deleted role shows \"None\". KI-79")]
    public async Task Level_WhenTheNearestRewardsRoleWasDeleted_ShowsTheNextReward()
    {
        var doomed = this._bot.Rest.CreateRole("doomed");
        await this.AddRewardAsync(doomed, 3);
        await this.AddRewardAsync(this.Guild.KickerRoleId, 5);
        this._bot.Rest.DeleteRole(doomed);
        await Task.Delay(TimeSpan.FromMilliseconds(200), Token);

        var embed = (await this._bot.RunSlashCommandAndWaitAsync("level", this.Guild.User1)).Embed!;

        Field(embed, "Next Reward").ShouldBe($"<@&{this.Guild.KickerRoleId}>\n at level 5");
    }

    /// <summary>
    ///     LVL-66: Top ignores the <c>user</c> option and shows the top of the list, not the rows around that user, who
    ///     ranks last of 21 here.
    /// </summary>
    [Fact]
    public async Task Leaderboard_Top_WithAUser_IgnoresTheUser()
    {
        var others = Enumerable.Range(1, 20).Select(i => new FakeUser(1_100_000_000_000_007_000 + (ulong)i, $"u{i}")).ToArray();
        await this.SeedXpAsync([.. others.Select((user, i) => (user, 1000 - i * 10)), (this.Guild.User1, 1)]);

        var reply = await this.LeaderboardAsync(LeaderboardOption.Top, this.Guild.User1);

        var lines = reply.Description!.Split('\n');
        lines.Length.ShouldBe(15);
        lines[0].ShouldBe($"**1** {others[0].Mention} **XP:** 1000");
        lines.ShouldNotContain(line => line.Contains(this.Guild.User1.Mention, StringComparison.Ordinal));
        reply.Embed!["footer"]!["text"]!.GetValue<string>().ShouldBe("Total Users 21");
    }

    /// <summary>LVL-71: a member whose XP was all reclaimed stays on the leaderboard, with 0 XP.</summary>
    [Fact]
    public async Task Leaderboard_KeepsAMemberReclaimedToZero()
    {
        await this.SeedXpAsync((this.Guild.User1, 10), (this.Guild.Moderator, 30));
        await this._bot.RunSlashCommandAndWaitAsync("reclaim", this.Guild.Moderator,
            SlashOption.User("user", this.Guild.User1), SlashOption.Enum("option", XpOption.All));
        await GrimoireTestBot.EventuallyAsync(this.XpOfUser1Async, xp => xp == 0);
        await this.RefreshLeaderboardAsync();

        var reply = await this.LeaderboardAsync(LeaderboardOption.Top);

        reply.Description.ShouldBe(string.Join('\n',
            $"**1** {this.Guild.Moderator.Mention} **XP:** 30",
            $"**2** {this.Guild.User1.Mention} **XP:** 0"));
    }

    private async Task<long> XpOfUser1Async()
    {
        await using var dbContext = database.CreateDbContext();
        return await dbContext.XpHistory
            .Where(xp => xp.UserId == new UserId(this.Guild.User1.Id) && xp.GuildId == this.GuildId)
            .SumAsync(xp => xp.RawXp, Token);
    }

    private Task<FakeReply> LeaderboardAsync(LeaderboardOption option, FakeUser? user = null)
        => this._bot.RunSlashCommandAndWaitAsync("leaderboard", this.Guild.User1,
        [
            SlashOption.Enum("option", option),
            .. user is null ? Array.Empty<SlashOption>() : [SlashOption.User("user", user)]
        ]);

    private async Task SeedXpAsync(params (FakeUser User, int Xp)[] entries)
    {
        await using (var seed = database.CreateDbContext())
        {
            seed.XpHistory.AddRange(entries.Select(entry => EarnedXp.Create(PositiveXpAmount.Create(entry.Xp).ShouldSucceed(),
                new UserId(entry.User.Id), this.GuildId, DateTimeOffset.UtcNow.AddMinutes(-10))));
            await seed.SaveChangesAsync(Token);
        }

        await this.RefreshLeaderboardAsync();
    }

    private async Task RefreshLeaderboardAsync()
    {
        await using var dbContext = database.CreateDbContext();
        await dbContext.Database.ExecuteSqlRawAsync("REFRESH MATERIALIZED VIEW leaderboard_view", Token);
    }

    private async Task AddRewardAsync(ulong roleId, int level)
        => (await this.Settings.SetRewardAsync(RewardAdded.Create(new RoleId(roleId), this.GuildId, level, null,
            new ModeratorId(this.Guild.Owner.Id), DateTimeOffset.UtcNow).ShouldSucceed())).ShouldSucceed();

    private static string Field(JsonNode embed, string name)
        => embed["fields"]!.AsArray().Single(field => field!["name"]!.GetValue<string>() == name)!["value"]!.GetValue<string>();
}
