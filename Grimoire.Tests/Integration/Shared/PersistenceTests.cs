// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text.Json.Nodes;
using Grimoire.Enums;
using Grimoire.Settings.Enums;
using Grimoire.Tests.Integration.FakeDiscord;
using LevelOption = Grimoire.Features.Leveling.Settings.LevelSettingsCommandGroup.LevelSettingsOptions;
using OverrideSetting = Grimoire.Features.Logging.Settings.LogSettingsCommands.Message.MessageLogOverrideSetting;

namespace Grimoire.Tests.Integration.Shared;

/// <summary>
///     Settings cache invalidation and restart persistence (backlog A-37). Each cache test reads a setting first, so
///     the cache holds it, changes it through a command, and checks the next read sees the change. The module cache is
///     covered by <c>ModuleCommandsTests</c>.
/// </summary>
[Collection("Test collection")]
public sealed class PersistenceTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private static readonly TimeSpan NothingHappensWindow = TimeSpan.FromSeconds(2);

    private readonly GrimoireCoreFactory _database = database;
    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await this._database.ResetDatabase();
        await this.StartAsync();
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    private async Task StartAsync()
    {
        this._bot = await GrimoireTestBot.StartAsync(this._database);
        await this._bot.ConfigureModerationAsync();
    }

    // ── Cache invalidation ────────────────────────────────────────────────────

    [Fact]
    public async Task ChangingTheXpAmount_AppliesToTheNextMessage()
    {
        await this._bot.SetModuleAsync(Module.Leveling, true);
        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "first");
        (await this.XpAsync(this.Guild.User1)).ShouldBe(5);

        await this.AdminAsync("level_settings", SlashOption.Subcommand("set", SlashOption.Enum("setting", LevelOption.Amount), SlashOption.Integer("value", 12)));
        this._bot.SendMessage(this.Guild.Moderator, this.Guild.General, "second");

        (await this.XpAsync(this.Guild.Moderator)).ShouldBe(12);
    }

    [Fact]
    public async Task AddingAReward_ShowsInTheNextLevelCheck()
    {
        await this._bot.SetModuleAsync(Module.Leveling, true);
        Field(await this.LevelEmbedAsync(), "Next Reward").ShouldBe("None");

        await this.AdminAsync("rewards", SlashOption.Subcommand("add", SlashOption.Role("role", this.Guild.KickerRoleId), SlashOption.Integer("level", 4)));

        Field(await this.LevelEmbedAsync(), "Next Reward").ShouldStartWith($"<@&{this.Guild.KickerRoleId}>");
    }

    [Fact]
    public async Task IgnoringAMember_AppliesToTheirNextMessage()
    {
        await this._bot.SetModuleAsync(Module.Leveling, true);
        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "first");
        (await this.XpAsync(this.Guild.User1)).ShouldBe(5);

        await this.AdminAsync("ignore", SlashOption.Subcommand("add", SlashOption.MentionableUser("items_0", this.Guild.Moderator)));
        this._bot.SendMessage(this.Guild.Moderator, this.Guild.General, "ignored");

        (await this.XpAsync(this.Guild.Moderator, NothingHappensWindow)).ShouldBe(0);
    }

    [Fact]
    public async Task ANeverOverride_AppliesToTheNextMessage()
    {
        await this._bot.ConfigureMessageLogAsync();
        (await this.SavedAsync(this._bot.SendMessage(this.Guild.User1, this.Guild.General, "logged"), TimeSpan.FromSeconds(10))).ShouldBeTrue();

        await this.AdminAsync("log", SlashOption.Group("message", SlashOption.Subcommand("override",
            SlashOption.Enum("option", OverrideSetting.Never), SlashOption.Channel("channel", this.Guild.General))));

        (await this.SavedAsync(this._bot.SendMessage(this.Guild.User1, this.Guild.General, "hidden"), NothingHappensWindow)).ShouldBeFalse();
    }

    // ── Restart ───────────────────────────────────────────────────────────────

    /// <summary>Settings made through commands are still there after the bot restarts on the same database.</summary>
    [Fact]
    public async Task Settings_SurviveARestart()
    {
        await this._bot.SetModuleAsync(Module.Leveling, true);
        await this.AdminAsync("level_settings", SlashOption.Subcommand("set", SlashOption.Enum("setting", LevelOption.Base), SlashOption.Integer("value", 30)));
        await this.AdminAsync("level_settings", SlashOption.Subcommand("log_set", SlashOption.Enum("option", ChannelOption.SelectChannel),
            SlashOption.Channel("channel", this.Guild.General)));
        await this.AdminAsync("rewards", SlashOption.Subcommand("add", SlashOption.Role("role", this.Guild.KickerRoleId), SlashOption.Integer("level", 4)));
        var before = (await this.AdminAsync("level_settings", SlashOption.Subcommand("view"))).Description;

        await this._bot.DisposeAsync();
        await this.StartAsync();

        (await this.AdminAsync("level_settings", SlashOption.Subcommand("view"))).Description.ShouldBe(before);
        before.ShouldNotBeNull().ShouldContain($"**Log-Channel:** {this.Guild.General.Mention}");
        (await this.AdminAsync("rewards", SlashOption.Subcommand("view"))).Description.ShouldNotBeNull()
            .ShouldContain($"Level:4 Role:<@&{this.Guild.KickerRoleId}>");
    }

    private Task<FakeReply> AdminAsync(string command, params SlashOption[] options)
        => this._bot.RunSlashCommandAndWaitAsync(command, this.Guild.Admin, options);

    private async Task<JsonNode> LevelEmbedAsync() => (await this._bot.RunSlashCommandAndWaitAsync("level", this.Guild.User1)).Embed!;

    private async Task<long> XpAsync(FakeUser user, TimeSpan? window = null)
        => await GrimoireTestBot.EventuallyAsync(async () =>
        {
            await using var dbContext = this._database.CreateDbContext();
            return (await dbContext.XpHistory.OfType<EarnedXp>().Where(xp => xp.UserId == new UserId(user.Id)).ToListAsync(Token))
                .Sum(xp => xp.Xp.Value);
        }, xp => xp > 0, window);

    private Task<bool> SavedAsync(ulong messageId, TimeSpan window)
        => GrimoireTestBot.EventuallyAsync(async () =>
        {
            await using var dbContext = this._database.CreateDbContext();
            return await dbContext.Messages.AnyAsync(message => message.Id == new MessageId(messageId), Token);
        }, saved => saved, window);

    private static string Field(JsonNode embed, string name)
        => embed["fields"]!.AsArray().Single(field => field!["name"]!.GetValue<string>() == name)!["value"]!.GetValue<string>();
}
