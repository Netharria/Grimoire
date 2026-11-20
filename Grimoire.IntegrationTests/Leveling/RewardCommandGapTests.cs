// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using Grimoire.IntegrationTests.FakeDiscord;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.IntegrationTests.Leveling;

/// <summary>
///     <c>/Rewards Add</c> cases from <c>06-leveling.md</c> section F that <see cref="RewardCommandsTests" /> doesn't
///     cover (backlog A-47): a level-0 reward and a blank message.
/// </summary>
[Collection("Test collection")]
public sealed class RewardCommandGapTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;
    private SettingsModule Settings => this._bot.Services.GetRequiredService<SettingsModule>();
    private GuildId GuildId => new(this.Guild.Id);

    public async ValueTask InitializeAsync()
    {
        await database.ResetDatabase();
        this._bot = await GrimoireTestBot.StartAsync(database);
        await this._bot.ConfigureModerationAsync();
        await this._bot.SetModuleAsync(Module.Leveling, true);
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    /// <summary>
    ///     LVL-102, Q41b (spec 2.5, OQ-3): the lowest reward level is 1, since every member is at least level 1. The
    ///     option is registered with that minimum, so Discord's client refuses 0, and a request that sends it anyway
    ///     fails DSharpPlus's own check before the command runs. Nothing is stored either way.
    /// </summary>
    [Fact]
    public async Task Add_AtLevelZero_IsRefused()
    {
        var level = this._bot.Rest.RegisteredCommands["rewards"]["options"]!.AsArray()
            .Single(option => option!["name"]!.GetValue<string>() == "add")!["options"]!.AsArray()
            .Single(option => option!["name"]!.GetValue<string>() == "level")!;
        level["min_value"]!.GetValue<int>().ShouldBe(1);

        (await this.AddAsync("add", SlashOption.Role("role", this.Guild.KickerRoleId), SlashOption.Integer("level", 0)))
            .Description.ShouldNotBeNull().ShouldNotContain("is now a reward");

        (await this.Settings.GetLevelingRewardsAsync(this.GuildId, TestContext.Current.CancellationToken))
            .ShouldSucceed().ShouldBeEmpty();
        // DSharpPlus's failed parameter check is logged by the command error handler, as for any out-of-range option.
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
