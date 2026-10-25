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

namespace Grimoire.Tests.Integration.Moderation;

/// <summary><c>/ModSettings</c> View, AutoPardon and PublicBanLog, cases SIN-50 to SIN-60 in <c>docs/testing/03-sins.md</c>.</summary>
[Collection("Test collection")]
public sealed class ModSettingsTests(GrimoireCoreFactory database) : IAsyncLifetime
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
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    /// <summary>SIN-50.</summary>
    [Fact]
    public async Task View_OnAFreshGuild_ShowsTheDefaults_Ephemerally()
    {
        var reply = await this.ModSettingsAsync(this.Guild.Admin, SlashOption.Subcommand("view"));

        reply.IsEphemeral.ShouldBeTrue();
        reply.Embed!["author"]!["name"]!.GetValue<string>().ShouldBe("Current moderation System Settings");
        reply.Description.ShouldBe("**Module Enabled:** True\n**Auto Pardon Duration:** 30 years\n**Ban Log:** None\n");
        this._bot.Rest.Unhandled.ShouldBeEmpty();
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>SIN-51.</summary>
    [Fact]
    public async Task View_ShowsTheConfiguredValues()
    {
        (await this.Settings.SetAutoPardonDuration(this.GuildId, new ModeratorId(this.Guild.Owner.Id), TimeSpan.FromDays(60), TestContext.Current.CancellationToken)).ShouldSucceed();
        (await this.Settings.SetLogChannelSetting(GuildLogType.PublicModeration, this.GuildId, new ModeratorId(this.Guild.Owner.Id), new ChannelId(this.Guild.General.Id), TestContext.Current.CancellationToken)).ShouldBeSetOrUnchanged();

        (await this.ModSettingsAsync(this.Guild.Admin, SlashOption.Subcommand("view"))).Description
            .ShouldBe($"**Module Enabled:** True\n**Auto Pardon Duration:** 2 months\n**Ban Log:** {this.Guild.General.Mention}\n");
    }

    /// <summary>SIN-52: each unit converts to days, and the change is logged.</summary>
    [Theory]
    [InlineData(Duration.Days, 10, 10)]
    [InlineData(Duration.Months, 3, 90)]
    [InlineData(Duration.Years, 2, 730)]
    public async Task AutoPardon_StoresTheDuration_AndLogsIt(Duration unit, int amount, int days)
    {
        (await this.AutoPardonAsync(unit, amount)).Description.ShouldBe($"Will now auto pardon sins after {amount} {unit}");

        (await this.Settings.GetAutoPardonDuration(this.GuildId, TestContext.Current.CancellationToken)).ShouldSucceed().ShouldBe(TimeSpan.FromDays(days));
        await this._bot.WaitForModLogEmbedAsync(embed => embed["description"]?.GetValue<string>()
            == $"{this.Guild.Admin.MemberMention} updated auto pardon to {amount} {unit}");
    }

    /// <summary>SIN-53: zero falls back to the 30-year default.</summary>
    [Fact]
    public async Task AutoPardon_OfZero_FallsBackToTheDefault()
    {
        await this.AutoPardonAsync(Duration.Days, 0);

        (await this.Settings.GetAutoPardonDuration(this.GuildId, TestContext.Current.CancellationToken)).ShouldSucceed().ShouldBe(TimeSpan.FromDays(10950));
    }

    /// <summary>SIN-54.</summary>
    [Fact(Skip = "Fails: a large number of years overflows TimeSpan, so the command errors instead of refusing. KI-35")]
    public async Task AutoPardon_TooLongToRepresent_IsRefusedClearly()
    {
        var reply = await this.AutoPardonAsync(Duration.Years, 1_000_000);

        reply.Description.ShouldNotBeNull().ShouldNotContain("error", Case.Insensitive);
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>SIN-55 and SIN-56.</summary>
    [Fact]
    public async Task PublicBanLog_SetAndOff_StoreAndLogTheChange()
    {
        var set = await this.PublicBanLogAsync(ChannelOption.SelectChannel, this.Guild.General);
        set.IsEphemeral.ShouldBeTrue();
        set.Description.ShouldBe($"Updated the public ban log to {this.Guild.General.Mention}");
        (await this.BanLogSettingAsync()).ShouldBe(this.Guild.General.Id);
        await this._bot.WaitForModLogEmbedAsync(embed => embed["description"]?.GetValue<string>()
            == $"{this.Guild.Admin.MemberMention} updated the public ban log to {this.Guild.General.Mention}.");

        (await this.PublicBanLogAsync(ChannelOption.Off)).Description.ShouldBe("Disabled the public ban log.");
        (await this.BanLogSettingAsync()).ShouldBeNull();
        await this._bot.WaitForModLogEmbedAsync(embed => embed["description"]?.GetValue<string>()
            == $"{this.Guild.Admin.MemberMention} disabled the public ban log.");
    }

    /// <summary>SIN-57.</summary>
    [Fact]
    public async Task PublicBanLog_WhereTheBotCannotSend_IsRefused()
    {
        await this._bot.DisposeAsync();
        this._bot = await GrimoireTestBot.StartAsync(this._database, guild => guild.BotPermissions =
            FakeGuild.ViewChannelPermission | FakeGuild.SendMessagesPermission | FakeGuild.EmbedLinksPermission);
        await this._bot.ConfigureModerationAsync();

        (await this.PublicBanLogAsync(ChannelOption.SelectChannel, this.Guild.Private)).Description
            .ShouldBe($"{this.Guild.Bot.MemberMention} don't have permission to send messages in that channel.");
        (await this.BanLogSettingAsync()).ShouldBeNull();
    }

    /// <summary>SIN-58.</summary>
    [Fact]
    public async Task PublicBanLog_SelectChannelWithoutAChannel_IsRejected()
    {
        (await this.PublicBanLogAsync(ChannelOption.SelectChannel)).Description
            .ShouldBe("Select a channel when using the Select Channel option.");
        (await this.BanLogSettingAsync()).ShouldBeNull();
    }

    /// <summary>SIN-59.</summary>
    [Theory]
    [InlineData("view")]
    [InlineData("auto_pardon")]
    [InlineData("public_ban_log")]
    public async Task ModSettings_NeedsManageGuild(string subcommand)
    {
        var options = subcommand switch
        {
            "auto_pardon" => SlashOption.Subcommand(subcommand, SlashOption.Enum("duration_type", Duration.Days), SlashOption.Integer("duration_amount", 5)),
            "public_ban_log" => SlashOption.Subcommand(subcommand, SlashOption.Enum("option", ChannelOption.Off)),
            _ => SlashOption.Subcommand(subcommand)
        };

        (await this.ModSettingsAsync(this.Guild.Moderator, options)).Description
            .ShouldBe("You do not have the required permissions to use this command.");
    }

    /// <summary>SIN-60.</summary>
    [Fact]
    public async Task ModSettings_WithModerationDisabled_IsRefused()
    {
        await this._bot.SetModuleAsync(Module.Moderation, false);

        (await this.ModSettingsAsync(this.Guild.Admin, SlashOption.Subcommand("view"))).Description
            .ShouldBe("This module is disabled in this server.");
    }

    private Task<FakeReply> ModSettingsAsync(FakeUser user, SlashOption subcommand)
        => this._bot.RunSlashCommandAndWaitAsync("mod_settings", user, subcommand);

    private Task<FakeReply> AutoPardonAsync(Duration unit, int amount)
        => this.ModSettingsAsync(this.Guild.Admin,
            SlashOption.Subcommand("auto_pardon", SlashOption.Enum("duration_type", unit), SlashOption.Integer("duration_amount", amount)));

    private Task<FakeReply> PublicBanLogAsync(ChannelOption option, FakeChannel? channel = null)
        => this.ModSettingsAsync(this.Guild.Admin, SlashOption.Subcommand("public_ban_log",
            channel is null
                ? [SlashOption.Enum("option", option)]
                : [SlashOption.Enum("option", option), SlashOption.Channel("channel", channel)]));

    private async Task<ulong?> BanLogSettingAsync()
        => (await this.Settings.GetLogChannelSetting(GuildLogType.PublicModeration, this.GuildId)).ShouldSucceed()?.Value;
}
