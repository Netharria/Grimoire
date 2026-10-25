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

namespace Grimoire.Tests.Integration.Shared;

/// <summary>
///     <c>/UserInfo</c>, section D of <c>docs/testing/01-general-settings.md</c>. GEN-103's second guild is stood in
///     for by XP rows with another guild id. GEN-119 (PluralKit) is in A-34.
/// </summary>
[Collection("Test collection")]
public sealed class UserInfoCommandTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private static readonly Module[] SectionModules = [Module.UserLog, Module.Leveling, Module.Moderation];

    /// <summary>The User Log section makes the command throw (KI-23), so most tests leave that module off.</summary>
    private static readonly Module[] WorkingSectionModules = [Module.Leveling, Module.Moderation];

    private const string UserLogThrows = "Fails: with the User Log module on, the nickname query can't be translated " +
                                         "by EF (it selects Nickname? then calls OfType<string>()), so the command throws. KI-23";

    private readonly GrimoireCoreFactory _database = database;
    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;
    private GuildId GuildId => new(this.Guild.Id);
    private SettingsModule Settings => this._bot.Services.GetRequiredService<SettingsModule>();

    public async ValueTask InitializeAsync()
    {
        await this._database.ResetDatabase();
        this._bot = await GrimoireTestBot.StartAsync(this._database);
        foreach (var module in WorkingSectionModules)
            await this._bot.SetModuleAsync(module, true);
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    /// <summary>GEN-100: the fields each section adds, in order, and what is always there.</summary>
    [Fact(Skip = UserLogThrows)]
    public async Task WithEverySectionEnabled_ShowsEveryField()
    {
        await this._bot.SetModuleAsync(Module.UserLog, true);

        var embed = await this.UserInfoAsync(this.Guild.User1);

        embed["author"]!["name"]!.GetValue<string>().ShouldBe("User info for user1");
        embed["thumbnail"]!["url"]!.GetValue<string>().ShouldNotBeNullOrEmpty();
        FieldNames(embed).ShouldBe(["Joined On", "Usernames", "Nicknames", "Level", "Can Gain Xp", "Earned Rewards", "Warns", "Mutes", "Bans"]);
        this._bot.Rest.Unhandled.ShouldBeEmpty();
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>GEN-100, GEN-114, GEN-115 and GEN-117: a member shows their join date, and their roles count.</summary>
    [Fact(Skip = "Fails: slash commands pass the user option as a DiscordUser, so UserInfo always takes the " +
                 "\"Not On Server\" path and ignores the member's roles. KI-19")]
    public async Task ForAMember_ShowsTheirJoinDate()
        => Field(await this.UserInfoAsync(this.Guild.User1), "Joined On").ShouldStartWith("<t:");

    /// <summary>GEN-101 and GEN-121: newest first, at most three, and empty nicknames left out.</summary>
    [Fact(Skip = UserLogThrows)]
    public async Task UsernamesAndNicknames_ShowTheNewestThree()
    {
        await this._bot.SetModuleAsync(Module.UserLog, true);
        var userId = new UserId(this.Guild.User1.Id);
        var start = DateTimeOffset.UtcNow.AddMinutes(1);
        await using (var seed = this._database.CreateDbContext())
        {
            foreach (var (name, minutes) in new[] { ("first", 1), ("second", 2), ("third", 3), ("fourth", 4) })
            {
                seed.UsernameHistory.Add(new UsernameHistory
                {
                    UserId = userId,
                    Username = Username.Create(name).ShouldSucceed(),
                    Timestamp = start.AddMinutes(minutes)
                });
                seed.NicknameHistory.Add(new NicknameHistory
                {
                    UserId = userId,
                    GuildId = this.GuildId,
                    Nickname = Nickname.Create($"nick-{name}").ShouldSucceed(),
                    Timestamp = start.AddMinutes(minutes)
                });
            }

            seed.NicknameHistory.Add(new NicknameHistory
            {
                UserId = userId,
                GuildId = this.GuildId,
                Nickname = null,
                Timestamp = start.AddMinutes(5)
            });
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var embed = await this.UserInfoAsync(this.Guild.User1);

        Field(embed, "Usernames").ShouldBe("fourth\nthird\nsecond");
        Field(embed, "Nicknames").ShouldBe("nick-fourth\nnick-third\nnick-second");
    }

    /// <summary>GEN-113 and GEN-118: a user with no history at all, who has never been in the server.</summary>
    [Fact]
    public async Task ForAUserWithNoHistory_ShowsUnknownsAndZeros()
    {
        var stranger = new FakeUser(1_100_000_000_000_000_099, "stranger");

        var embed = await this.UserInfoAsync(stranger);

        Field(embed, "Joined On").ShouldBe("Not On Server");
        Field(embed, "Level").ShouldBe((await this.LevelForXpAsync(0)).ToString());
        Field(embed, "Earned Rewards").ShouldBe("None");
        new[] { "Warns", "Mutes", "Bans" }.Select(name => Field(embed, name)).ShouldAllBe(count => count == "0");
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>GEN-102 and GEN-103: the level comes from this guild's XP only.</summary>
    [Fact]
    public async Task Level_CountsOnlyThisGuildsXp()
    {
        await this.SeedXpAsync(this.GuildId, 120, 80);
        await this.SeedXpAsync(new GuildId(1_100_000_000_000_000_999), 100_000);

        Field(await this.UserInfoAsync(this.Guild.User1), "Level").ShouldBe((await this.LevelForXpAsync(200)).ToString());
    }

    /// <summary>GEN-104 for an ignored member.</summary>
    [Fact]
    public async Task CanGainXp_IsNoForAnIgnoredMember_AndYesOtherwise()
    {
        Field(await this.UserInfoAsync(this.Guild.User1), "Can Gain Xp").ShouldBe("Yes");

        var ignored = IgnoredMember.Create(new UserId(this.Guild.User1.Id), this.GuildId, new ModeratorId(this.Guild.Owner.Id),
            DateTimeOffset.UtcNow).ShouldSucceed();
        (await this.Settings.AppendIgnoredItemsEvent(this.GuildId, new HashSet<XpTrackedItem> { ignored }, TestContext.Current.CancellationToken)).ShouldSucceed();

        Field(await this.UserInfoAsync(this.Guild.User1), "Can Gain Xp").ShouldBe("No");
    }

    /// <summary>GEN-104 for an ignored role. The member's roles never reach the check.</summary>
    [Fact(Skip = "Fails: the user option arrives as a DiscordUser, so the member's roles are empty and a role " +
                 "ignore is never seen. KI-19")]
    public async Task CanGainXp_IsNoForAMemberWithAnIgnoredRole()
    {
        var ignored = IgnoredRole.Create(new RoleId(this.Guild.ModeratorRoleId), this.GuildId, new ModeratorId(this.Guild.Owner.Id),
            DateTimeOffset.UtcNow).ShouldSucceed();
        (await this.Settings.AppendIgnoredItemsEvent(this.GuildId, new HashSet<XpTrackedItem> { ignored }, TestContext.Current.CancellationToken)).ShouldSucceed();

        Field(await this.UserInfoAsync(this.Guild.Moderator), "Can Gain Xp").ShouldBe("No");
    }

    /// <summary>GEN-105 and GEN-106: rewards at or below the member's level are listed, others are not.</summary>
    [Fact]
    public async Task EarnedRewards_ListsOnlyRewardsAtOrBelowTheMembersLevel()
    {
        await this.SeedXpAsync(this.GuildId, 5_000);
        var level = await this.LevelForXpAsync(5_000);
        level.ShouldBeGreaterThan(1, "the seeded XP should reach a level above 1");
        await this.AddRewardAsync(this.Guild.ManagerRoleId, level);
        await this.AddRewardAsync(this.Guild.AdminRoleId, level + 1);

        Field(await this.UserInfoAsync(this.Guild.User1), "Earned Rewards").ShouldBe($"<@&{this.Guild.ManagerRoleId}>");
    }

    /// <summary>
    ///     GEN-107 and GEN-108: counts per sin type, limited to the auto-pardon window. Pardoned sins inside the
    ///     window still count; A-14 checks whether that matches <c>/SinLog</c>.
    /// </summary>
    [Fact]
    public async Task SinCounts_ArePerType_AndExcludeSinsOlderThanTheAutoPardonWindow()
    {
        (await this.Settings.SetAutoPardonDuration(this.GuildId, new ModeratorId(this.Guild.Owner.Id), TimeSpan.FromDays(30), TestContext.Current.CancellationToken))
            .ShouldSucceed();
        var now = DateTimeOffset.UtcNow;
        await this.SeedSinsAsync(
            (SinType.Warn, now.AddDays(-1)), (SinType.Warn, now.AddDays(-2)), (SinType.Mute, now.AddDays(-3)),
            (SinType.Ban, now.AddDays(-4)), (SinType.Warn, now.AddDays(-40)));

        var embed = await this.UserInfoAsync(this.Guild.User1);

        Field(embed, "Warns").ShouldBe("2");
        Field(embed, "Mutes").ShouldBe("1");
        Field(embed, "Bans").ShouldBe("1");
    }

    /// <summary>GEN-109 to GEN-111: a section appears only while its module is on.</summary>
    [Theory]
    [InlineData(Module.Leveling, new[] { "Level", "Can Gain Xp", "Earned Rewards" })]
    [InlineData(Module.Moderation, new[] { "Warns", "Mutes", "Bans" })]
    public async Task EachSection_AppearsOnlyWithItsModule(Module module, string[] fields)
    {
        foreach (var other in WorkingSectionModules.Where(other => other != module))
            await this._bot.SetModuleAsync(other, false);

        FieldNames(await this.UserInfoAsync(this.Guild.User1)).ShouldBe(["Joined On", .. fields]);
    }

    /// <summary>GEN-109: the User Log section, which currently makes the command throw.</summary>
    [Fact(Skip = UserLogThrows)]
    public async Task UserLogSection_AppearsWithItsModule()
    {
        foreach (var other in WorkingSectionModules)
            await this._bot.SetModuleAsync(other, false);
        await this._bot.SetModuleAsync(Module.UserLog, true);

        FieldNames(await this.UserInfoAsync(this.Guild.User1)).ShouldBe(["Joined On", "Usernames", "Nicknames"]);
    }

    /// <summary>GEN-112.</summary>
    [Fact]
    public async Task WithEverySectionDisabled_ShowsOnlyTheJoinDate()
    {
        foreach (var module in WorkingSectionModules)
            await this._bot.SetModuleAsync(module, false);

        FieldNames(await this.UserInfoAsync(this.Guild.User1)).ShouldBe(["Joined On"]);
    }

    /// <summary>GEN-116.</summary>
    [Fact]
    public async Task ForTheBot_Works()
    {
        (await this.UserInfoAsync(this.Guild.Bot))["author"]!["name"]!.GetValue<string>().ShouldBe("User info for Grimoire");
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>GEN-08 and GEN-120: UserInfo needs ManageGuild, which the moderator role doesn't have.</summary>
    [Theory]
    [InlineData("mod")]
    [InlineData("user1")]
    public async Task WithoutManageGuild_IsDenied(string who)
        => (await this._bot.RunSlashCommandAndWaitAsync("user_info", who == "mod" ? this.Guild.Moderator : this.Guild.User1,
                SlashOption.User("user", this.Guild.User1)))
            .Description.ShouldBe("You do not have the required permissions to use this command.");

    private async Task<JsonNode> UserInfoAsync(FakeUser target)
    {
        var reply = await this._bot.RunSlashCommandAndWaitAsync("user_info", this.Guild.Admin, SlashOption.User("user", target));
        return reply.Embed ?? throw new InvalidOperationException($"No embed in reply: {reply.Message.ToJsonString()}");
    }

    private static string[] FieldNames(JsonNode embed)
        => [.. (embed["fields"]?.AsArray() ?? []).Select(field => field!["name"]!.GetValue<string>())];

    private static string Field(JsonNode embed, string name)
        => embed["fields"]!.AsArray().Single(field => field!["name"]!.GetValue<string>() == name)!["value"]!.GetValue<string>();

    private async Task<int> LevelForXpAsync(long xp)
        => (await this.Settings.GetLevelingSettings(this.GuildId)).ShouldSucceed().GetLevelFromXp(xp);

    private async Task SeedXpAsync(GuildId guildId, params long[] amounts)
    {
        await using var seed = this._database.CreateDbContext();
        foreach (var amount in amounts)
            seed.XpHistory.Add(EarnedXp.Create(PositiveXpAmount.Create(amount).ShouldSucceed(), new UserId(this.Guild.User1.Id),
                guildId, DateTimeOffset.UtcNow).ShouldSucceed());
        await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task AddRewardAsync(ulong roleId, int level)
        => (await this.Settings.SetRewardAsync(RewardAdded.Create(new RoleId(roleId), this.GuildId, level, null,
            new ModeratorId(this.Guild.Owner.Id), DateTimeOffset.UtcNow).ShouldSucceed())).ShouldSucceed();

    private async Task SeedSinsAsync(params (SinType Type, DateTimeOffset At)[] sins)
    {
        await using var seed = this._database.CreateDbContext();
        var actor = new ModerationActor.Moderator(new ModeratorId(this.Guild.Moderator.Id));
        var userId = new UserId(this.Guild.User1.Id);
        foreach (var (type, at) in sins)
            seed.Sins.Add((type switch
            {
                SinType.Warn => Sin.ForWarn(actor, userId, this.GuildId, at),
                SinType.Mute => Sin.ForMute(actor, userId, this.GuildId, at),
                SinType.Ban => Sin.ForBan(actor, userId, this.GuildId, at),
                _ => Sin.ForKick(actor, userId, this.GuildId, at)
            }).ShouldSucceed());
        await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
