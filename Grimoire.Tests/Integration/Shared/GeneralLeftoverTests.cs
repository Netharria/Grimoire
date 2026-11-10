// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text.Json.Nodes;
using Grimoire.Enums;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using Grimoire.Tests.Integration.FakeDiscord;
using Microsoft.Extensions.DependencyInjection;
using ModuleArgument = Grimoire.Features.Shared.Commands.ModuleCommands.ModuleArguments;

namespace Grimoire.Tests.Integration.Shared;

/// <summary>
///     Area 01 cases left over after A-01 to A-04 (backlog A-39): the user command channel bypass, purging a pinned
///     message, and how general settings and module states survive a restart, a direct database edit, the bot leaving
///     and rejoining, and two writes at once.
/// </summary>
[Collection("Test collection")]
public sealed class GeneralLeftoverTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private readonly GrimoireCoreFactory _database = database;
    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;
    private SettingsModule Settings => this._bot.Services.GetRequiredService<SettingsModule>();
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

    /// <summary>GEN-57: Manage Messages bypasses the user command channel, so a moderator's /Level elsewhere is public.</summary>
    [Fact]
    public async Task Level_ByAModerator_OutsideTheUserCommandChannel_IsPublic()
    {
        await this._bot.SetModuleAsync(Module.Leveling, true);
        (await this.Settings.SetUserCommandChannelSetting(new GuildId(this.Guild.Id), new ModeratorId(this.Guild.Owner.Id),
            new ChannelId(this.Guild.General.Id), Token)).ShouldBeSetOrUnchanged();

        (await this._bot.RunSlashCommandAndWaitAsync(this.Guild.ModLog, "level", this.Guild.Moderator)).IsEphemeral.ShouldBeFalse();
        (await this._bot.RunSlashCommandAndWaitAsync(this.Guild.ModLog, "level", this.Guild.User1)).IsEphemeral
            .ShouldBeTrue("a member outside the user command channel gets a private reply");
    }

    /// <summary>GEN-144: purge has no pin exclusion, so a pinned message in range is deleted with the rest.</summary>
    [Fact]
    public async Task PurgeAll_DeletesPinnedMessagesToo()
    {
        var ids = this._bot.Rest.SeedHistory(this.Guild.General,
            [.. Enumerable.Range(0, 5).Select(i => (this.Guild.User1, DateTimeOffset.UtcNow.AddMinutes(-60 + i)))]);
        this._bot.Rest.PinInHistory(this.Guild.General, ids[2]);

        (await this._bot.RunSlashCommandAndWaitAsync("purge", this.Guild.Moderator,
            SlashOption.Subcommand("all", SlashOption.Integer("count", 5)))).Description.ShouldBe("Purged 5 messages");

        var bulk = this._bot.Rest.Requests.Single(request => request.Method == HttpMethod.Post
                                                             && request.Path == $"channels/{this.Guild.General.Id}/messages/bulk-delete");
        bulk.Json!["messages"]!.AsArray().Select(id => ulong.Parse(id!.ToString())).ShouldContain(ids[2]);
        this._bot.Rest.HistoryIds(this.Guild.General).ShouldBeEmpty();
    }

    /// <summary>GEN-170: general settings and module states set by command are unchanged after a restart.</summary>
    [Fact]
    public async Task GeneralSettingsAndModules_SurviveARestart()
    {
        (await this.AdminAsync("general_settings", SlashOption.Subcommand("user_commands",
                SlashOption.Enum("option", ChannelOption.SelectChannel), SlashOption.Channel("channel", this.Guild.General))))
            .Description.ShouldNotBeNull().ShouldContain(this.Guild.General.Mention);
        await this.SetModuleAsync(ModuleArgument.Leveling, true);
        var settings = (await this.AdminAsync("general_settings", SlashOption.Subcommand("view"))).Description;
        var modules = await this.ModulesViewAsync();

        await this._bot.DisposeAsync();
        await this.StartAsync();

        settings.ShouldBe($"**Moderation Log:** {this.Guild.ModLog.Mention}\n**User Command Channel:** {this.Guild.General.Mention}");
        (await this.AdminAsync("general_settings", SlashOption.Subcommand("view"))).Description.ShouldBe(settings);
        modules.ShouldContain("**Leveling Enabled:** True");
        (await this.ModulesViewAsync()).ShouldBe(modules);
    }

    /// <summary>
    ///     GEN-171: a setting edited straight in the database isn't seen while the old value is cached, and is seen
    ///     after a restart.
    /// </summary>
    [Fact]
    public async Task AModuleStateEditedInTheDatabase_IsStaleUntilARestart()
    {
        await this.SetModuleAsync(ModuleArgument.Leveling, true);
        (await this.ModulesViewAsync()).ShouldContain("**Leveling Enabled:** True");

        var settingType = Module.Leveling.ToGuildSettingType()!.Value;
        await using (var dbContext = this._database.CreateSettingsDbContext())
            (await dbContext.GuildSettings.OfType<GuildSettingCustomValue>()
                .Where(setting => setting.GuildId == new GuildId(this.Guild.Id) && setting.Type == settingType)
                .ExecuteUpdateAsync(update => update.SetProperty(setting => setting.Value, "False"), Token)).ShouldBe(1);

        (await this.ModulesViewAsync()).ShouldContain("**Leveling Enabled:** True", customMessage: "the cached value is still read");

        await this._bot.DisposeAsync();
        await this.StartAsync();

        (await this.ModulesViewAsync()).ShouldContain("**Leveling Enabled:** False");
    }

    /// <summary>GEN-172: after the bot leaves a guild and is added back, /Modules View works and shows the old state.</summary>
    [Fact]
    public async Task AfterTheBotLeavesAndRejoins_ModulesViewStillWorks()
    {
        await this.SetModuleAsync(ModuleArgument.Leveling, true);

        this._bot.Gateway.Dispatch("GUILD_DELETE", new JsonObject { ["id"] = FakeUser.Snowflake(this.Guild.Id), ["unavailable"] = false });
        await Task.Delay(TimeSpan.FromMilliseconds(300), Token);
        this._bot.Gateway.Dispatch("GUILD_CREATE", this.Guild.GuildCreatePayload());
        await Task.Delay(TimeSpan.FromMilliseconds(500), Token);

        (await this.ModulesViewAsync()).ShouldContain("**Leveling Enabled:** True");
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>
    ///     GEN-173: two admins setting the same module at once both get an answer, nothing errors, and the module ends
    ///     in the state of the newest stored row.
    /// </summary>
    [Fact]
    public async Task TwoModuleSetsAtOnce_BothComplete_AndTheNewestWriteWins()
    {
        var enable = this._bot.RunSlashCommand("modules", this.Guild.Admin, SetOptions(ModuleArgument.Leveling, true));
        var disable = this._bot.RunSlashCommand("modules", this.Guild.Owner, SetOptions(ModuleArgument.Leveling, false));

        (await FakeReply.WaitForAsync(this._bot.Rest, enable)).Description.ShouldBe("Enabled Leveling");
        (await FakeReply.WaitForAsync(this._bot.Rest, disable)).Description.ShouldBe("Disabled Leveling");

        var settingType = Module.Leveling.ToGuildSettingType()!.Value;
        await using var dbContext = this._database.CreateSettingsDbContext();
        var newest = await dbContext.GuildSettings
            .Where(setting => setting.GuildId == new GuildId(this.Guild.Id) && setting.Type == settingType)
            .OrderByDescending(setting => setting.SetAt)
            .FirstAsync(Token);
        var enabled = newest is GuildSettingCustomValue { Value: var value } && bool.Parse(value);
        (await this.ModulesViewAsync()).ShouldContain($"**Leveling Enabled:** {enabled}");
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    private static SlashOption SetOptions(ModuleArgument module, bool enable)
        => SlashOption.Subcommand("set", SlashOption.Enum("module", module), SlashOption.Boolean("enable", enable));

    private async Task SetModuleAsync(ModuleArgument module, bool enable)
        => (await this.AdminAsync("modules", SetOptions(module, enable))).Description
            .ShouldBe($"{(enable ? "Enabled" : "Disabled")} {module}");

    private async Task<string> ModulesViewAsync()
        => (await this.AdminAsync("modules", SlashOption.Subcommand("view"))).Description.ShouldNotBeNull();

    private Task<FakeReply> AdminAsync(string command, params SlashOption[] options)
        => this._bot.RunSlashCommandAndWaitAsync(command, this.Guild.Admin, options);
}
