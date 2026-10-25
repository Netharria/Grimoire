// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Enums;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using Grimoire.Tests.Integration.FakeDiscord;
using Microsoft.Extensions.DependencyInjection;
using Option = Grimoire.Features.Leveling.Settings.LevelSettingsCommandGroup.LevelSettingsOptions;

namespace Grimoire.Tests.Integration.Leveling;

/// <summary><c>/LevelSettings</c> View, Set and LogSet (backlog A-27).</summary>
[Collection("Test collection")]
public sealed class LevelSettingsTests(GrimoireCoreFactory database) : IAsyncLifetime
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

    // ── View ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task View_ShowsTheModuleTimeoutAndLogChannel()
    {
        var reply = await this.LevelSettingsAsync("view");

        reply.Embed!["author"]!["name"]!.GetValue<string>().ShouldBe("Current Level System Settings");
        reply.Description.ShouldNotBeNull().ShouldStartWith("**Module Enabled:** True\n**Text Time:** 3 minutes.\n");
        reply.Description.ShouldEndWith("**Log-Channel:** None\n");
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    [Fact(Skip = "Fails: Base, Modifier and Reward Amount are value types without a ToString override, so View " +
                 "shows \"LevelScalingBase { Value = 15 }\" instead of 15. KI-43")]
    public async Task View_ShowsTheNumbersPlainly()
        => (await this.LevelSettingsAsync("view")).Description.ShouldNotBeNull()
            .ShouldContain("**Base:** 15\n**Modifier:** 50\n**Reward Amount:** 5\n");

    // ── Set ───────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(Option.TextTime, 10)]
    [InlineData(Option.Base, 30)]
    [InlineData(Option.Modifier, 75)]
    [InlineData(Option.Amount, 12)]
    public async Task Set_StoresTheValue_AndLogsIt(Option option, int value)
    {
        (await this.SetAsync(option, value)).Description.ShouldBe($"Updated {option} level setting to {value}");

        var settings = (await this.Settings.GetLevelingSettings(this.GuildId)).ShouldSucceed();
        (option switch
        {
            Option.TextTime => (int)settings.XpTimeoutPeriod.Value.TotalMinutes,
            Option.Base => settings.Base.Value,
            Option.Modifier => settings.Modifier.Value,
            _ => settings.Amount.Value
        }).ShouldBe(value);
        await this._bot.WaitForModLogEmbedAsync(e => e["description"]?.GetValue<string>()
            == $"{this.Guild.Admin.MemberMention} updated {option} level setting to {value}");
    }

    [Theory]
    [InlineData(Option.TextTime, 61, "Xp timeout period must be a value between 1 and 60 in minutes.")]
    [InlineData(Option.Base, 501, "Level scaling base must be a value between 1 and 500.")]
    [InlineData(Option.Modifier, 201, "Level scaling modifier must be a value between 1 and 200.")]
    [InlineData(Option.Amount, 101, "Xp gain amount must be a value between 1 and 100.")]
    public async Task Set_OutOfRange_IsRejected_AndNothingChanges(Option option, int value, string error)
    {
        (await this.SetAsync(option, value)).Description.ShouldBe(error);

        var settings = (await this.Settings.GetLevelingSettings(this.GuildId)).ShouldSucceed();
        (settings.Base.Value, settings.Modifier.Value, settings.Amount.Value, settings.XpTimeoutPeriod.Value.TotalMinutes)
            .ShouldBe((15, 50, 5, 3d));
    }

    // ── LogSet ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task LogSet_StoresTheChannel_ViewShowsIt_AndOffClearsIt()
    {
        (await this.LogSetAsync(ChannelOption.SelectChannel, this.Guild.General)).Description
            .ShouldBe($"Updated the level log to {this.Guild.General.Mention}");
        (await this.LogChannelAsync()).ShouldBe(this.Guild.General.Id);
        await this._bot.WaitForModLogEmbedAsync(e => e["description"]?.GetValue<string>()
            == $"{this.Guild.Admin.MemberMention} updated the level log to {this.Guild.General.Mention}.");
        (await this.LevelSettingsAsync("view")).Description.ShouldNotBeNull()
            .ShouldEndWith($"**Log-Channel:** {this.Guild.General.Mention}\n");

        (await this.LogSetAsync(ChannelOption.Off)).Description.ShouldBe("Disabled the level log.");
        (await this.LogChannelAsync()).ShouldBeNull();
    }

    [Fact]
    public async Task LogSet_WhereTheBotCannotSend_IsRefused()
    {
        await this._bot.DisposeAsync();
        this._bot = await GrimoireTestBot.StartAsync(this._database, guild => guild.BotPermissions =
            FakeGuild.ViewChannelPermission | FakeGuild.SendMessagesPermission | FakeGuild.EmbedLinksPermission);

        (await this.LogSetAsync(ChannelOption.SelectChannel, this.Guild.Private)).Description
            .ShouldBe($"{this.Guild.Bot.MemberMention} does not have permissions to send messages in that channel.");
        (await this.LogChannelAsync()).ShouldBeNull();
    }

    // ── Gates ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("permission")]
    [InlineData("module")]
    public async Task LevelSettings_NeedManageGuildAndTheModule(string missing)
    {
        if (missing == "module")
            await this._bot.SetModuleAsync(Module.Leveling, false);

        (await this._bot.RunSlashCommandAndWaitAsync("level_settings", missing == "module" ? this.Guild.Admin : this.Guild.Moderator,
                SlashOption.Subcommand("view")))
            .Description.ShouldBe(missing == "module"
                ? "This module is disabled in this server."
                : "You do not have the required permissions to use this command.");
    }

    private Task<FakeReply> LevelSettingsAsync(string subcommand, params SlashOption[] options)
        => this._bot.RunSlashCommandAndWaitAsync("level_settings", this.Guild.Admin, SlashOption.Subcommand(subcommand, options));

    private Task<FakeReply> SetAsync(Option option, int value)
        => this.LevelSettingsAsync("set", SlashOption.Enum("setting", option), SlashOption.Integer("value", value));

    private Task<FakeReply> LogSetAsync(ChannelOption option, FakeChannel? channel = null)
        => this.LevelSettingsAsync("log_set",
        [
            SlashOption.Enum("option", option),
            .. channel is null ? Array.Empty<SlashOption>() : [SlashOption.Channel("channel", channel)]
        ]);

    private async Task<ulong?> LogChannelAsync()
        => (await this.Settings.GetLogChannelSetting(GuildLogType.Leveling, this.GuildId)).ShouldSucceed()?.Value;
}
