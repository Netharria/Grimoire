// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings.Domain;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using Grimoire.Tests.Integration.FakeDiscord;
using Microsoft.Extensions.DependencyInjection;
using ModuleArgument = Grimoire.Features.Shared.Commands.ModuleCommands.ModuleArguments;

namespace Grimoire.Tests.Integration.Shared;

/// <summary>
///     <c>/Modules View</c> and <c>/Modules Set</c>, section C of <c>docs/testing/01-general-settings.md</c>.
///     GEN-71 (restart persistence) is covered in A-37, and GEN-72 and GEN-85 (two guilds) in A-35.
/// </summary>
[Collection("Test collection")]
public sealed class ModuleCommandsTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private static readonly TimeSpan NothingHappensWindow = TimeSpan.FromSeconds(2);

    private readonly GrimoireCoreFactory _database = database;
    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;

    public async ValueTask InitializeAsync()
    {
        await this._database.ResetDatabase();
        this._bot = await GrimoireTestBot.StartAsync(this._database);
        await this._bot.ConfigureModerationAsync();
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    /// <summary>Module names as strings: the command's enum is internal, so it can't be a public test parameter.</summary>
    public static TheoryData<string> EveryModule
        => ["Leveling", "UserLog", "Moderation", "MessageLog", "Commands", "AntiSpam"];

    /// <summary>GEN-70 and GEN-73: six modules are listed and General, which is always on, is not.</summary>
    [Fact]
    public async Task View_ListsEachToggleableModuleWithItsState()
    {
        var reply = await this._bot.RunSlashCommandAndWaitAsync("modules", this.Guild.Admin, SlashOption.Subcommand("view"));

        reply.IsEphemeral.ShouldBeFalse();
        reply.Embed!["author"]!["name"]!.GetValue<string>().ShouldBe("Current states of modules.");
        reply.Description.ShouldBe(
            "**Leveling Enabled:** False\n**User Log Enabled:** False\n**Message Log Enabled:** False\n" +
            "**Moderation Enabled:** True\n**Commands Enabled:** False\n**Anti Spam Enabled:** False\n");
        this._bot.Logs.Errors.ShouldBeEmpty();
        this._bot.Rest.Unhandled.ShouldBeEmpty();
    }

    /// <summary>
    ///     GEN-80, GEN-81 and GEN-89: the reply, the stored state and who set it, and the mod-log line. The mod log is
    ///     gated on the always-on General module, so disabling Moderation is still logged.
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryModule))]
    public async Task Set_TogglesTheModule_RecordsWhoDidIt_AndLogsIt(string moduleName)
    {
        var module = Enum.Parse<ModuleArgument>(moduleName);
        // Start every module off. Setting the current state stores nothing new, so each step below is a real change.
        await this._bot.SetModuleAsync(Enum.Parse<Module>(moduleName), false);
        (await this.SetAsync(module, true)).Description.ShouldBe($"Enabled {module}");
        (await this.StoredStateAsync(module)).ShouldBe((true, this.Guild.Admin.Id));
        await this._bot.WaitForModLogEmbedAsync(embed =>
            embed["description"]?.GetValue<string>() == $"{this.Guild.Admin.Username} Enabled {module}");

        (await this.SetAsync(module, false)).Description.ShouldBe($"Disabled {module}");
        (await this.StoredStateAsync(module)).ShouldBe((false, this.Guild.Admin.Id));
        // GEN-89: the mod log names the user by username, not by mention like other commands do.
        await this._bot.WaitForModLogEmbedAsync(embed =>
            embed["description"]?.GetValue<string>() == $"{this.Guild.Admin.Username} Disabled {module}");
    }

    /// <summary>GEN-82: setting the current state again is not an error, and is logged again.</summary>
    [Fact]
    public async Task Set_ToTheCurrentState_RepliesAndLogsAgain()
    {
        (await this.SetAsync(ModuleArgument.Leveling, true)).Description.ShouldBe("Enabled Leveling");
        (await this.SetAsync(ModuleArgument.Leveling, true)).Description.ShouldBe("Enabled Leveling");

        var logged = await GrimoireTestBot.EventuallyAsync(
            () => Task.FromResult(this._bot.Rest.Requests.Count(this._bot.IsModLogPost)), count => count >= 2);
        logged.ShouldBe(2);
        (await this.StoredStateAsync(ModuleArgument.Leveling)).Enabled.ShouldBeTrue();
    }

    /// <summary>GEN-83: the settings cache is invalidated on write, so View shows the change at once.</summary>
    [Theory]
    [MemberData(nameof(EveryModule))]
    public async Task View_RightAfterSet_ShowsTheNewState(string moduleName)
    {
        var module = Enum.Parse<ModuleArgument>(moduleName);
        await this.SetAsync(module, module != ModuleArgument.Moderation);

        var reply = await this._bot.RunSlashCommandAndWaitAsync("modules", this.Guild.Admin, SlashOption.Subcommand("view"));

        var label = module switch
        {
            ModuleArgument.UserLog => "User Log",
            ModuleArgument.MessageLog => "Message Log",
            ModuleArgument.AntiSpam => "Anti Spam",
            _ => module.ToString()
        };
        reply.Description.ShouldNotBeNull().ShouldContain($"**{label} Enabled:** {module != ModuleArgument.Moderation}");
    }

    /// <summary>GEN-84: with no mod-log channel set, Set still works and nothing is posted or logged as an error.</summary>
    [Fact]
    public async Task Set_WithNoModLogChannel_WorksQuietly()
    {
        (await this._bot.Services.GetRequiredService<SettingsModule>()
            .SetLogChannelSetting(GuildLogType.Moderation, new GuildId(this.Guild.Id), new ModeratorId(this.Guild.Owner.Id), null, TestContext.Current.CancellationToken))
            .ShouldSucceed();

        (await this.SetAsync(ModuleArgument.Leveling, true)).Description.ShouldBe("Enabled Leveling");

        (await this._bot.Rest.ReceivesRequestWithinAsync(this._bot.IsModLogPost, NothingHappensWindow)).ShouldBeFalse();
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>GEN-87.</summary>
    [Fact]
    public async Task Set_ByAModeratorWithoutManageGuild_IsDenied_AndChangesNothing()
    {
        var reply = await this._bot.RunSlashCommandAndWaitAsync("modules", this.Guild.Moderator, SetOptions(ModuleArgument.Leveling, true));

        reply.Description.ShouldBe("You do not have the required permissions to use this command.");
        (await this.StoredStateAsync(ModuleArgument.Leveling)).Enabled.ShouldBeFalse();
    }

    /// <summary>GEN-88 for the message log; the leveling and user-log halves are in A-25 and A-23.</summary>
    [Fact]
    public async Task DisabledMessageLog_SavesNoMessages_AndEnablingItStartsSaving()
    {
        var ignored = this._bot.SendMessage(this.Guild.User1, this.Guild.General, "while disabled");
        (await this.MessageSavedWithinAsync(ignored, NothingHappensWindow)).ShouldBeFalse();

        await this.SetAsync(ModuleArgument.MessageLog, true);

        var saved = this._bot.SendMessage(this.Guild.User1, this.Guild.General, "while enabled");
        (await this.MessageSavedWithinAsync(saved, TimeSpan.FromSeconds(10))).ShouldBeTrue();
    }

    private static SlashOption SetOptions(ModuleArgument module, bool enable)
        => SlashOption.Subcommand("set", SlashOption.Enum("module", module), SlashOption.Boolean("enable", enable));

    private Task<FakeReply> SetAsync(ModuleArgument module, bool enable)
        => this._bot.RunSlashCommandAndWaitAsync("modules", this.Guild.Admin, SetOptions(module, enable));

    /// <summary>The newest stored setting for the module: whether it is on, and who set it.</summary>
    private async Task<(bool Enabled, ulong SetBy)> StoredStateAsync(ModuleArgument module)
    {
        var settingType = Enum.Parse<Module>(module.ToString()).ToGuildSettingType()!.Value;
        await using var dbContext = this._database.CreateSettingsDbContext();
        var latest = await dbContext.GuildSettings
            .Where(setting => setting.GuildId == new GuildId(this.Guild.Id) && setting.Type == settingType)
            .OrderByDescending(setting => setting.SetAt)
            .FirstOrDefaultAsync();
        // A module that was never set has no row and is off.
        return latest is null
            ? (false, 0)
            : (latest is GuildSettingCustomValue { Value: var value } && bool.Parse(value), latest.SetBy.Value);
    }

    private async Task<bool> MessageSavedWithinAsync(ulong messageId, TimeSpan window)
        => await GrimoireTestBot.EventuallyAsync(async () =>
        {
            await using var dbContext = this._database.CreateDbContext();
            return await dbContext.Messages.AnyAsync(message => message.Id == new MessageId(messageId));
        }, saved => saved, window);
}
