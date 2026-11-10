// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using Grimoire.Tests.Integration.FakeDiscord;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Tests.Integration.Leveling;

/// <summary>
///     <c>/Rewards Add</c> cases from <c>06-leveling.md</c> section F that <see cref="RewardCommandsTests" /> doesn't
///     cover (backlog A-47): a level-0 reward and a blank message.
/// </summary>
[Collection("Test collection")]
public sealed class RewardCommandGapTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private readonly GrimoireCoreFactory _database = database;
    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;
    private SettingsModule Settings => this._bot.Services.GetRequiredService<SettingsModule>();
    private GuildId GuildId => new(this.Guild.Id);

    public async ValueTask InitializeAsync()
    {
        await this._database.ResetDatabase();
        this._bot = await GrimoireTestBot.StartAsync(this._database);
        await this._bot.ConfigureModerationAsync();
        await this._bot.SetModuleAsync(Module.Leveling, true);
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    /// <summary>LVL-102: a level-0 reward is accepted and granted on a member's next earned message.</summary>
    [Fact]
    public async Task Add_AtLevelZero_IsGrantedOnTheNextEarnedMessage()
    {
        (await this.AddAsync("add", SlashOption.Role("role", this.Guild.KickerRoleId), SlashOption.Integer("level", 0)))
            .Description.ShouldBe($"Successfully updated the rewards to include <@&{this.Guild.KickerRoleId}> at level 0.");

        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "hello");

        (await GrimoireTestBot.EventuallyAsync(() => Task.FromResult(this.Guild.GetMember(this.Guild.User1).RoleIds),
            roles => roles.Contains(this.Guild.KickerRoleId))).ShouldContain(this.Guild.KickerRoleId);
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>LVL-108: a message of only spaces is refused, and nothing is stored.</summary>
    [Fact]
    public async Task Add_WithABlankMessage_IsRefused()
    {
        (await this.AddAsync("add", SlashOption.Role("role", this.Guild.KickerRoleId), SlashOption.Integer("level", 2),
                SlashOption.String("message", "   ")))
            .Description.ShouldBe("Was not able to update the rewards for the server:\n" +
                                  "Reward message must be 1–4096 non-whitespace characters.");

        (await this.Settings.GetLevelingRewardsAsync(this.GuildId, cancellationToken: TestContext.Current.CancellationToken)).ShouldSucceed().ShouldBeEmpty();
    }

    private Task<FakeReply> AddAsync(string subcommand, params SlashOption[] options)
        => this._bot.RunSlashCommandAndWaitAsync("rewards", this.Guild.Admin, SlashOption.Subcommand(subcommand, options));
}
