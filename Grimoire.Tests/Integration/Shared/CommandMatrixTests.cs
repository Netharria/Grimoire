// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.SharedDtos;
using Grimoire.Settings.Enums;
using Grimoire.Tests.Integration.FakeDiscord;

namespace Grimoire.Tests.Integration.Shared;

/// <summary>
///     Matrices over every command (backlog A-36). The first locks in each command's registered
///     <c>default_member_permissions</c>, which is what Discord uses to decide who sees it. Only a
///     <c>RequirePermissions</c> user list sets it; <c>RequireUserGuildPermissions</c> is checked by the bot alone, so
///     most commands are shown to everyone and refused bot-side. The second runs each module-gated command with its
///     module off and expects the standard refusal.
/// </summary>
[Collection("Test collection")]
public sealed class CommandMatrixTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private const string Everyone = "2147483648";
    private const string ManageMessages = "8192";
    private const string ModuleDisabled = "This module is disabled in this server.";

    private readonly GrimoireCoreFactory _database = database;
    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;

    public async ValueTask InitializeAsync()
    {
        await this._database.ResetDatabase();
        this._bot = await GrimoireTestBot.StartAsync(this._database);
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    /// <summary>Who Discord shows each command to. Null means Discord's default, which is also everyone.</summary>
    public static TheoryData<string, string?> Registrations => new()
    {
        { "award", Everyone }, { "ban", null }, { "command", Everyone }, { "commands", Everyone },
        { "diagnostics", Everyone }, { "forget", Everyone }, { "general_settings", Everyone }, { "ignore", Everyone },
        { "leaderboard", Everyone }, { "level", Everyone }, { "level_settings", Everyone }, { "lock", ManageMessages },
        { "log", Everyone }, { "mod_settings", Everyone }, { "modules", Everyone }, { "mute", null }, { "mutes", null },
        { "pardon", Everyone }, { "publish", Everyone }, { "purge", ManageMessages }, { "reason", Everyone },
        { "reclaim", Everyone }, { "rewards", Everyone }, { "sin_log", Everyone }, { "spam_filter", Everyone },
        { "unban", null }, { "unlock", ManageMessages }, { "unmute", null }, { "user_info", Everyone },
        { "warn", ManageMessages }
    };

    [Theory]
    [MemberData(nameof(Registrations))]
    public void EachCommand_IsRegisteredForTheExpectedMembers(string command, string? permissions)
        => this._bot.Rest.RegisteredCommands[command]["default_member_permissions"]?.ToString().ShouldBe(permissions);

    /// <summary>No command is registered that the table above doesn't know about (<c>/Kick</c> is missing, KI-25).</summary>
    [Fact]
    public void EveryRegisteredCommand_IsInTheTable()
        => this._bot.Rest.RegisteredCommands.Keys.Order().ShouldBe(
            Registrations.Select(row => row.Data.Item1).Order(), ignoreOrder: false);

    public static TheoryData<string, Module> ModuleGated => new()
    {
        { "warn", Module.Moderation }, { "ban", Module.Moderation }, { "unban", Module.Moderation },
        { "mute", Module.Moderation }, { "unmute", Module.Moderation }, { "mutes view", Module.Moderation },
        { "lock", Module.Moderation }, { "unlock", Module.Moderation }, { "publish ban", Module.Moderation },
        { "forget", Module.Moderation }, { "pardon", Module.Moderation }, { "reason", Module.Moderation },
        { "sin_log", Module.Moderation }, { "mod_settings view", Module.Moderation }, { "diagnostics check", Module.Moderation },
        { "level", Module.Leveling }, { "leaderboard", Module.Leveling }, { "award", Module.Leveling },
        { "reclaim", Module.Leveling }, { "rewards add", Module.Leveling }, { "rewards view", Module.Leveling },
        { "ignore add", Module.Leveling }, { "ignore remove", Module.Leveling }, { "level_settings view", Module.Leveling },
        { "commands learn", Module.Commands }, { "commands forget", Module.Commands }, { "commands history", Module.Commands },
        { "commands revert", Module.Commands }, { "commands stats", Module.Commands }, { "commands leaderboard", Module.Commands },
        { "command", Module.Commands }, { "log message view", Module.MessageLog }, { "log user view", Module.UserLog }
    };

    /// <summary>
    ///     Each module-gated command refuses when its module is off, even for an administrator. <c>/Rewards Remove</c>
    ///     and <c>/Ignore View</c> have no gate (KI-45) and are left out.
    /// </summary>
    [Theory]
    [MemberData(nameof(ModuleGated))]
    public async Task EachModuleGatedCommand_RefusesWithItsModuleOff(string command, Module module)
    {
        foreach (var other in Enum.GetValues<Module>().Where(other => other != module && other != Module.General))
            await this._bot.SetModuleAsync(other, true);
        await this._bot.SetModuleAsync(module, false);

        var reply = await this._bot.RunSlashCommandAndWaitAsync(this.Guild.General, command.Split(' ')[0], this.Guild.Admin, this.OptionsFor(command));

        reply.Description.ShouldBe(ModuleDisabled);
    }

    private SlashOption[] OptionsFor(string command)
    {
        var user = SlashOption.User("user", this.Guild.User1);
        var sinId = SlashOption.String("sin_id", "1");
        return command switch
        {
            "warn" => [user, SlashOption.String("reason", "x")],
            "ban" or "unban" or "unmute" => [user],
            "mute" => [user, SlashOption.Enum("duration_type", DurationType.Hours), SlashOption.Integer("duration_amount", 1)],
            "lock" => [SlashOption.Enum("duration_type", DurationType.Hours), SlashOption.Integer("duration_amount", 1)],
            "command" => [SlashOption.String("command_name", "hello")],
            "publish ban" => [SlashOption.Subcommand("ban", sinId)],
            "forget" => [sinId],
            "pardon" or "reason" => [sinId, SlashOption.String("reason", "x")],
            "sin_log" => [SlashOption.Enum("type", Grimoire.Features.Moderation.SinAdmin.Commands.SinQueryType.All)],
            "leaderboard" => [SlashOption.Enum("option", Grimoire.Features.Leveling.UserCommands.GetLeaderboard.LeaderboardOption.Top)],
            "award" => [user, SlashOption.Integer("xp", 5)],
            "reclaim" => [user, SlashOption.Enum("option", Grimoire.Features.Leveling.Awards.ReclaimUserXp.XpOption.All)],
            "rewards add" => [SlashOption.Subcommand("add", SlashOption.Role("role", this.Guild.KickerRoleId), SlashOption.Integer("level", 1))],
            "ignore add" => [SlashOption.Subcommand("add", SlashOption.MentionableUser("items_0", this.Guild.User1))],
            "ignore remove" => [SlashOption.Subcommand("remove", SlashOption.MentionableUser("item_0", this.Guild.User1))],
            "commands learn" => [SlashOption.Subcommand("learn", SlashOption.String("name", "hello"), SlashOption.String("content", "hi"))],
            "commands forget" or "commands history" or "commands stats"
                => [SlashOption.Subcommand(command.Split(' ')[1], SlashOption.String("name", "hello"))],
            "commands revert" => [SlashOption.Subcommand("revert", SlashOption.String("name", "hello"), SlashOption.String("version", "1"))],
            "log message view" => [SlashOption.Group("message", SlashOption.Subcommand("view"))],
            "log user view" => [SlashOption.Group("user", SlashOption.Subcommand("view"))],
            _ when command.Contains(' ') => [SlashOption.Subcommand(command.Split(' ')[1])],
            _ => []
        };
    }
}
