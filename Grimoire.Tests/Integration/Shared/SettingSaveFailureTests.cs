// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Enums;
using Grimoire.Features.Moderation.SpamFilter.Commands;
using Grimoire.Settings.Enums;
using Grimoire.Tests.Integration.FakeDiscord;
using LevelOption = Grimoire.Features.Leveling.Settings.LevelSettingsCommandGroup.LevelSettingsOptions;
using MessageLogSetting = Grimoire.Features.Logging.Settings.LogSettingsCommands.Message.MessageLogSetting;
using MessageOverride = Grimoire.Features.Logging.Settings.LogSettingsCommands.Message.MessageLogOverrideSetting;
using ModuleArgument = Grimoire.Features.Shared.Commands.ModuleCommands.ModuleArguments;
using UserLogSetting = Grimoire.Features.Logging.Settings.LogSettingsCommands.User.UserLogSetting;

namespace Grimoire.Tests.Integration.Shared;

/// <summary>
///     I-4: a setting command whose save fails must say so, privately, and not announce the change in the mod log.
///     The settings tables are made to refuse every insert, while reads, including the mod log lookup, still work.
/// </summary>
[Collection("Test collection")]
public sealed class SettingSaveFailureTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private const string SettingSaveFailed = "Was not able to save the setting to the database.";
    private static readonly TimeSpan NothingHappensWindow = TimeSpan.FromSeconds(2);

    private readonly GrimoireCoreFactory _database = database;
    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;

    public static TheoryData<string, string> Commands => new()
    {
        { "modules set", SettingSaveFailed },
        { "general_settings mod_log_channel", SettingSaveFailed },
        { "general_settings user_commands", SettingSaveFailed },
        { "mutes set", SettingSaveFailed },
        { "mod_settings auto_pardon", SettingSaveFailed },
        { "mod_settings public_ban_log", SettingSaveFailed },
        { "level_settings set", SettingSaveFailed },
        { "level_settings log_set", SettingSaveFailed },
        { "log message set", SettingSaveFailed },
        { "log user set", SettingSaveFailed },
        { "log message override", "Could not set channel log override." },
        { "spam_filter override", "Could not save the spam filter override." }
    };

    public async ValueTask InitializeAsync()
    {
        await this._database.ResetDatabase();
        this._bot = await GrimoireTestBot.StartAsync(this._database);
        await this._bot.ConfigureModerationAsync();
        foreach (var module in new[] { Module.Leveling, Module.MessageLog, Module.UserLog, Module.AntiSpam })
            await this._bot.SetModuleAsync(module, true);
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    /// <remarks>Cases: SPAM-51 (spam_filter override).</remarks>
    [Theory]
    [MemberData(nameof(Commands))]
    public async Task ASettingThatCannotBeSaved_IsReportedPrivately_AndNotLogged(string command, string error)
    {
        var muteRoleId = this._bot.Rest.CreateRole("Muted");

        var reply = await this.WhileSettingsCannotBeSavedAsync(() => this.RunAsync(command, muteRoleId));

        reply.Description.ShouldBe(error);
        reply.IsEphemeral.ShouldBeTrue();
        (await this._bot.Rest.ReceivesRequestWithinAsync(this._bot.IsModLogPost, NothingHappensWindow)).ShouldBeFalse();
    }

    /// <summary>The role already exists by then, so the reply says so and points at the command that can retry.</summary>
    [Fact]
    public async Task MutesCreate_WhenTheRoleCannotBeSaved_SaysSo_AndStops()
    {
        var followUp = await this.WhileSettingsCannotBeSavedAsync(async () =>
        {
            var interaction = this._bot.RunSlashCommand("mutes", this.Guild.Admin, SlashOption.Subcommand("create"));
            return await this._bot.Rest.WaitForRequestAsync(interaction.IsFollowup);
        });

        var description = followUp.Json!["embeds"]![0]!["description"]!.GetValue<string>();
        description.ShouldStartWith("Created <@&");
        description.ShouldEndWith($"but couldn't save it as the mute role: {SettingSaveFailed} Use /Mutes Set to try again.");
        (await this._bot.Rest.ReceivesRequestWithinAsync(this._bot.IsModLogPost, NothingHappensWindow)).ShouldBeFalse();
    }

    private Task<FakeReply> RunAsync(string command, ulong muteRoleId)
    {
        var general = this.Guild.General;
        return command switch
        {
            "modules set" => this.Run("modules", SlashOption.Subcommand("set",
                SlashOption.Enum("module", ModuleArgument.Leveling), SlashOption.Boolean("enable", false))),
            "general_settings mod_log_channel" => this.Run("general_settings",
                ChannelSetting("mod_log_channel", "channel", general)),
            "general_settings user_commands" => this.Run("general_settings",
                ChannelSetting("user_commands", "channel", general)),
            "mutes set" => this.Run("mutes", SlashOption.Subcommand("set", SlashOption.Role("role", muteRoleId))),
            "mod_settings auto_pardon" => this.Run("mod_settings", SlashOption.Subcommand("auto_pardon",
                SlashOption.Enum("duration_type", Duration.Days), SlashOption.Integer("duration_amount", 3))),
            "mod_settings public_ban_log" => this.Run("mod_settings",
                ChannelSetting("public_ban_log", "channel", general)),
            "level_settings set" => this.Run("level_settings", SlashOption.Subcommand("set",
                SlashOption.Enum("setting", LevelOption.Amount), SlashOption.Integer("value", 42))),
            "level_settings log_set" => this.Run("level_settings", ChannelSetting("log_set", "channel", general)),
            "log message set" => this.Run("log", SlashOption.Group("message",
                ChannelSetting("set", "value", general, SlashOption.Enum("setting", MessageLogSetting.DeleteLog)))),
            "log user set" => this.Run("log", SlashOption.Group("user",
                ChannelSetting("set", "value", general, SlashOption.Enum("setting", UserLogSetting.JoinLog)))),
            "log message override" => this.Run("log", SlashOption.Group("message", SlashOption.Subcommand("override",
                SlashOption.Enum("option", MessageOverride.Never), SlashOption.Channel("channel", general)))),
            "spam_filter override" => this.Run("spam_filter", SlashOption.Subcommand("override",
                SlashOption.Enum("option", SpamFilterOverrideSetting.Never), SlashOption.Channel("channel", general))),
            _ => throw new ArgumentOutOfRangeException(nameof(command), command, null)
        };
    }

    private Task<FakeReply> Run(string commandName, SlashOption subcommand)
        => this._bot.RunSlashCommandAndWaitAsync(commandName, this.Guild.Admin, subcommand);

    private static SlashOption ChannelSetting(string subcommand, string channelOption, FakeChannel channel,
        params SlashOption[] extra)
        => SlashOption.Subcommand(subcommand,
        [
            .. extra, SlashOption.Enum("option", ChannelOption.SelectChannel),
            SlashOption.Channel(channelOption, channel)
        ]);

    /// <summary>Makes every settings table that a command writes refuse inserts until <paramref name="action" /> ends.</summary>
    private async Task<T> WhileSettingsCannotBeSavedAsync<T>(Func<Task<T>> action)
    {
        await using var refusing = await this._database.RefuseSettingsInsertsAsync(
            "GuildSetting", "MessageLogChannelOverride", "SpamFilterOverride");
        return await action();
    }
}
