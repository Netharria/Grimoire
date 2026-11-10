// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text.Json.Nodes;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using Grimoire.Tests.Integration.FakeDiscord;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Tests.Integration.Shared;

/// <summary>
///     I-5 and X-5: a settings lookup that fails is handled, not turned into a <c>NullReferenceException</c>. A table
///     is renamed to make its reads fail. Anything the command checks first, such as whether its module is on, is read
///     before that, so it comes from the cache.
/// </summary>
[Collection("Test collection")]
public sealed class SettingLookupFailureTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;
    private GuildId GuildId => new(this.Guild.Id);
    private SettingsModule Settings => this._bot.Services.GetRequiredService<SettingsModule>();

    public async ValueTask InitializeAsync()
    {
        await database.ResetDatabase();
        this._bot = await GrimoireTestBot.StartAsync(database);
        await this._bot.SetModuleAsync(Module.Leveling, true);
        await this._bot.SetModuleAsync(Module.Moderation, true);
        foreach (var module in new[] { Module.Leveling, Module.Moderation, Module.UserLog })
            await this.Settings.IsModuleEnabled(module, this.GuildId, TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    [Fact]
    public async Task ModulesView_WhenTheModulesCannotBeRead_SaysSoPrivately()
    {
        var reply = await this.WhileUnreadableAsync("GuildSetting", () =>
            this._bot.RunSlashCommandAndWaitAsync("modules", this.Guild.Admin, SlashOption.Subcommand("view")));

        reply.Description.ShouldBe("Could not load the module states. Please try again later.");
        reply.IsEphemeral.ShouldBeTrue();
        this.ShouldNotHaveCrashed();
    }

    [Fact]
    public async Task LevelSettingsView_WhenTheSettingsCannotBeRead_SaysSoPrivately()
    {
        var reply = await this.WhileUnreadableAsync("GuildSetting", () =>
            this._bot.RunSlashCommandAndWaitAsync("level_settings", this.Guild.Admin, SlashOption.Subcommand("view")));

        reply.Description.ShouldBe("Could not load the leveling settings. Please try again later.");
        reply.IsEphemeral.ShouldBeTrue();
        this.ShouldNotHaveCrashed();
    }

    [Fact]
    public async Task RewardsView_WhenTheRewardsCannotBeRead_SaysSoPrivately()
    {
        var reply = await this.WhileUnreadableAsync("Reward", () =>
            this._bot.RunSlashCommandAndWaitAsync("rewards", this.Guild.Admin, SlashOption.Subcommand("view")));

        reply.Description.ShouldBe("Could not load the rewards. Please try again later.");
        reply.IsEphemeral.ShouldBeTrue();
        this.ShouldNotHaveCrashed();
    }

    [Fact]
    public async Task Level_WhenTheRewardsCannotBeRead_StillShowsTheLevel()
    {
        var reply = await this.WhileUnreadableAsync("Reward", () =>
            this._bot.RunSlashCommandAndWaitAsync("level", this.Guild.User1));

        Field(reply.Embed!, "Level").ShouldBe("1");
        Field(reply.Embed!, "Next Reward").ShouldBe("Unavailable");
        this.ShouldNotHaveCrashed();
    }

    [Fact]
    public async Task UserInfo_WhenTheRewardsCannotBeRead_LeavesOutOnlyTheLevelFields()
    {
        var reply = await this.WhileUnreadableAsync("Reward", () =>
            this._bot.RunSlashCommandAndWaitAsync("user_info", this.Guild.Admin, SlashOption.User("user", this.Guild.User1)));

        FieldNames(reply.Embed!).ShouldBe(["Joined On", "Warns", "Mutes", "Bans"]);
        this.ShouldNotHaveCrashed();
    }

    [Fact]
    public async Task GainingXp_WhenTheRewardsCannotBeRead_KeepsTheXp_AndDoesNotCrash()
    {
        await this.WhileUnreadableAsync("Reward", async () =>
        {
            this._bot.SendMessage(this.Guild.User1, this.Guild.General, "hello");
            // The rewards lookup is the last read, after the XP is saved. Its failure is logged before it returns.
            return await this._bot.Logs.WaitForAsync(logEvent => logEvent.RenderMessage().StartsWith(
                "Settings module operation failed", StringComparison.Ordinal), TimeSpan.FromSeconds(10));
        });
        await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);

        await using var dbContext = database.CreateDbContext();
        (await dbContext.XpHistory.CountAsync(xp => xp.UserId == new UserId(this.Guild.User1.Id), TestContext.Current.CancellationToken))
            .ShouldBe(1);
        this.ShouldNotHaveCrashed();
    }

    /// <summary>A null in place of the failed value surfaces as either, depending on whether LINQ or the code reads it.</summary>
    private void ShouldNotHaveCrashed()
        => this._bot.Logs.Events.Select(logEvent => logEvent.Exception)
            .Where(exception => exception is NullReferenceException or ArgumentNullException)
            .ShouldBeEmpty();

    private static string[] FieldNames(JsonNode embed)
        => [.. (embed["fields"]?.AsArray() ?? []).Select(field => field!["name"]!.GetValue<string>())];

    private static string Field(JsonNode embed, string name)
        => embed["fields"]!.AsArray().Single(field => field!["name"]!.GetValue<string>() == name)!["value"]!.GetValue<string>();

    private async Task<T> WhileUnreadableAsync<T>(string entityName, Func<Task<T>> action)
    {
        await using var unreadable = await database.MakeSettingsTablesUnreadableAsync(entityName);
        return await action();
    }
}
