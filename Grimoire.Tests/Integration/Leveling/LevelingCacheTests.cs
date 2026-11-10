// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings.Domain;
using Grimoire.Settings.Enums;
using Grimoire.Tests.Integration.FakeDiscord;
using Option = Grimoire.Features.Leveling.Settings.LevelSettingsCommandGroup.LevelSettingsOptions;

namespace Grimoire.Tests.Integration.Leveling;

/// <summary>
///     The leveling cross-checks in <c>06-leveling.md</c> section I (backlog A-47): what the settings cache keeps, and
///     what a command or a module toggle changes straight away. Each test warms the cache with a first message.
/// </summary>
[Collection("Test collection")]
public sealed class LevelingCacheTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private static readonly TimeSpan NothingHappensWindow = TimeSpan.FromSeconds(2);

    private readonly GrimoireCoreFactory _database = database;
    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;
    private GuildId GuildId => new(this.Guild.Id);
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await this._database.ResetDatabase();
        await this.StartAsync();
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    private async Task StartAsync()
    {
        if (this._bot is not null)
            await this._bot.DisposeAsync();
        this._bot = await GrimoireTestBot.StartAsync(this._database);
        await this._bot.ConfigureModerationAsync();
        await this._bot.SetModuleAsync(Module.Leveling, true);
    }

    /// <summary>
    ///     LVL-170: a setting written straight to the database, bypassing the bot, isn't seen until the bot restarts,
    ///     because the cached settings are only cleared by the bot's own saves.
    /// </summary>
    [Fact]
    public async Task ASettingChangedInTheDatabase_IsOnlySeenAfterARestart()
    {
        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "warm the cache");
        (await this.EarnedAsync(this.Guild.User1, 1)).ShouldHaveSingleItem().Xp.Value.ShouldBe(5);

        await using (var settings = this._database.CreateSettingsDbContext())
        {
            settings.GuildSettings.Add(GuildSettingCustomValue.Create(GuildSettingType.XpGainAmount, this.GuildId,
                new ModeratorId(this.Guild.Owner.Id), DateTimeOffset.UtcNow, "40").ShouldSucceed());
            await settings.SaveChangesAsync(Token);
        }

        this._bot.SendMessage(this.Guild.Moderator, this.Guild.General, "still cached");
        (await this.EarnedAsync(this.Guild.Moderator, 1)).ShouldHaveSingleItem().Xp.Value.ShouldBe(5);

        await this.StartAsync();
        this._bot.SendMessage(this.Guild.Admin, this.Guild.General, "after the restart");
        (await this.EarnedAsync(this.Guild.Admin, 1)).ShouldHaveSingleItem().Xp.Value.ShouldBe(40);
    }

    /// <summary>LVL-171: a value set through <c>/LevelSettings Set</c> applies to the very next message.</summary>
    [Fact]
    public async Task ASettingSetByCommand_AppliesToTheNextMessage()
    {
        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "warm the cache");
        await this.EarnedAsync(this.Guild.User1, 1);

        await this._bot.RunSlashCommandAndWaitAsync("level_settings", this.Guild.Admin,
            SlashOption.Subcommand("set", SlashOption.Enum("setting", Option.TextTime), SlashOption.Integer("value", 20)));
        this._bot.SendMessage(this.Guild.Moderator, this.Guild.General, "straight after");

        (await this.EarnedAsync(this.Guild.Moderator, 1)).ShouldHaveSingleItem().TimeOut
            .ShouldBe(DateTimeOffset.UtcNow.AddMinutes(20), TimeSpan.FromSeconds(30));
    }

    /// <summary>LVL-172: a reward and an ignore added by command both apply to the next XP gain.</summary>
    [Fact]
    public async Task ARewardAndAnIgnoreAddedByCommand_BothApplyAtOnce()
    {
        this._bot.SendMessage(this.Guild.Moderator, this.Guild.General, "warm the cache");
        await this.EarnedAsync(this.Guild.Moderator, 1);

        await this._bot.RunSlashCommandAndWaitAsync("rewards", this.Guild.Admin,
            SlashOption.Subcommand("add", SlashOption.Role("role", this.Guild.KickerRoleId), SlashOption.Integer("level", 1)));
        await this._bot.RunSlashCommandAndWaitAsync("ignore", this.Guild.Admin,
            SlashOption.Subcommand("add", SlashOption.MentionableUser("items_0", this.Guild.User1)));
        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "ignored now");
        this._bot.SendMessage(this.Guild.Admin, this.Guild.General, "rewarded now");

        await GrimoireTestBot.EventuallyAsync(() => Task.FromResult(this.Guild.GetMember(this.Guild.Admin).RoleIds),
            roles => roles.Contains(this.Guild.KickerRoleId));
        this.Guild.GetMember(this.Guild.Admin).RoleIds.ShouldContain(this.Guild.KickerRoleId);
        (await this.EarnedAsync(this.Guild.User1, 1, NothingHappensWindow)).ShouldBeEmpty();
        this.Guild.GetMember(this.Guild.User1).RoleIds.ShouldNotContain(this.Guild.KickerRoleId);
    }

    /// <summary>
    ///     LVL-173: after the module is turned off and on again, members earn again, and a timeout from before still
    ///     applies.
    /// </summary>
    [Fact]
    public async Task TurningTheModuleOffAndOn_KeepsTheTimeout_AndOthersEarnAgain()
    {
        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "before");
        await this.EarnedAsync(this.Guild.User1, 1);

        await this._bot.SetModuleAsync(Module.Leveling, false);
        await this._bot.SetModuleAsync(Module.Leveling, true);
        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "still in the timeout");
        this._bot.SendMessage(this.Guild.Moderator, this.Guild.General, "earns again");

        (await this.EarnedAsync(this.Guild.Moderator, 1)).ShouldHaveSingleItem();
        (await this.EarnedAsync(this.Guild.User1, 2, NothingHappensWindow)).Count.ShouldBe(1);
    }

    private Task<List<EarnedXp>> EarnedAsync(FakeUser user, int count, TimeSpan? window = null)
        => GrimoireTestBot.EventuallyAsync(async () =>
        {
            await using var dbContext = this._database.CreateDbContext();
            return await dbContext.XpHistory.OfType<EarnedXp>()
                .Where(xp => xp.UserId == new UserId(user.Id) && xp.GuildId == this.GuildId).ToListAsync(Token);
        }, earned => earned.Count >= count, window);
}
