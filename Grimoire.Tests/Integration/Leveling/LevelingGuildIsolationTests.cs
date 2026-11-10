// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings.Enums;
using Grimoire.Tests.Integration.FakeDiscord;
using XpOption = Grimoire.Features.Leveling.Awards.ReclaimUserXp.XpOption;

namespace Grimoire.Tests.Integration.Leveling;

/// <summary>
///     Leveling command cases from <c>06-leveling.md</c> sections F to H that need two guilds (backlog A-47): rewards,
///     ignores and reclaimed XP stay in the guild they belong to. Guild A is <c>Test-A</c>, B is <c>Test-B</c>.
/// </summary>
[Collection("Test collection")]
public sealed class LevelingGuildIsolationTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private readonly GrimoireCoreFactory _database = database;
    private GrimoireTestBot _bot = null!;

    private FakeGuild A => this._bot.Guild;
    private FakeGuild B => this._bot.SecondGuild!;
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await this._database.ResetDatabase();
        this._bot = await GrimoireTestBot.StartAsync(this._database, secondGuild: true);
        await this._bot.ConfigureModerationAsync();
        await this._bot.ConfigureModerationAsync(this.B);
        await this._bot.SetModuleAsync(Module.Leveling, true);
        await this._bot.SetModuleAsync(Module.Leveling, true, this.B);
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    /// <summary>LVL-118: rewards added in B don't show in A's View.</summary>
    [Fact]
    public async Task RewardsInB_DoNotShowInA()
    {
        (await this._bot.RunSlashCommandAndWaitAsync(this.B.General, "rewards", this.B.Admin,
                SlashOption.Subcommand("add", SlashOption.Role("role", this.B.KickerRoleId), SlashOption.Integer("level", 2))))
            .Description.ShouldBe($"Successfully updated the rewards to include <@&{this.B.KickerRoleId}> at level 2.");

        (await this._bot.RunSlashCommandAndWaitAsync(this.A.General, "rewards", this.A.Admin, SlashOption.Subcommand("view")))
            .Description.ShouldBe("This server does not have any rewards.");
    }

    /// <summary>LVL-134: a member ignored in A still earns in B.</summary>
    [Fact]
    public async Task AMemberIgnoredInA_StillEarnsInB()
    {
        await this._bot.RunSlashCommandAndWaitAsync(this.A.General, "ignore", this.A.Admin,
            SlashOption.Subcommand("add", SlashOption.MentionableUser("items_0", this.A.User1)));

        this._bot.SendMessage(this.A.User1, this.B.General, "hello from B");

        (await this.EarnedRowsAsync(this.B, count => count >= 1)).ShouldBe(1);
        this._bot.SendMessage(this.A.User1, this.A.General, "hello from A");
        (await this.EarnedRowsAsync(this.A, count => count >= 1, TimeSpan.FromSeconds(2))).ShouldBe(0);
    }

    /// <summary>LVL-157: Reclaim All in A only takes A's XP.</summary>
    [Fact]
    public async Task ReclaimAllInA_OnlyCountsAsXp()
    {
        await using (var seed = this._database.CreateDbContext())
        {
            seed.XpHistory.Add(EarnedXp.Create(PositiveXpAmount.Create(20).ShouldSucceed(), new UserId(this.A.User1.Id),
                new GuildId(this.A.Id), DateTimeOffset.UtcNow.AddMinutes(-10)));
            seed.XpHistory.Add(EarnedXp.Create(PositiveXpAmount.Create(50).ShouldSucceed(), new UserId(this.A.User1.Id),
                new GuildId(this.B.Id), DateTimeOffset.UtcNow.AddMinutes(-10)));
            await seed.SaveChangesAsync(Token);
        }

        (await this._bot.RunSlashCommandAndWaitAsync(this.A.General, "reclaim", this.A.Moderator,
                SlashOption.User("user", this.A.User1), SlashOption.Enum("option", XpOption.All)))
            .Description.ShouldBe($"20 xp has been taken from {this.A.User1.Mention}.");

        await using var dbContext = this._database.CreateDbContext();
        (await dbContext.XpHistory.Where(xp => xp.GuildId == new GuildId(this.A.Id)).SumAsync(xp => xp.RawXp, Token)).ShouldBe(0);
        (await dbContext.XpHistory.Where(xp => xp.GuildId == new GuildId(this.B.Id)).SumAsync(xp => xp.RawXp, Token)).ShouldBe(50);
    }

    private Task<int> EarnedRowsAsync(FakeGuild guild, Func<int, bool> isDone, TimeSpan? window = null)
        => GrimoireTestBot.EventuallyAsync(async () =>
        {
            await using var dbContext = this._database.CreateDbContext();
            return await dbContext.XpHistory.OfType<EarnedXp>()
                .CountAsync(xp => xp.GuildId == new GuildId(guild.Id) && xp.UserId == new UserId(this.A.User1.Id), Token);
        }, isDone, window);
}
