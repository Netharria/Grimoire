// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Domain.Shared;
using Grimoire.Settings.Enums;
using JetBrains.Annotations;

namespace Grimoire.Features.Shared.Commands;

internal sealed partial class ModuleCommands
{
    [UsedImplicitly]
    [Command("Set")]
    [Description("Enable or Disable a module.")]
    public async Task SetAsync(SlashCommandContext ctx,
        [Parameter("Module")] [Description("The module to enable or disable.")]
        Module module,
        [Parameter("Enable")] [Description("Whether to enable or disable the module.")]
        bool enable)
    {
        await ctx.DeferResponseAsync();

        if (ctx.Guild is null)
            throw new AnticipatedException("This command can only be used in a server.");

        var settings = await this._settingsModule.GetGuildSettings(ctx.Guild.Id);

        if (settings is null)
            throw new AnticipatedException("Guild settings not found.");

        // ReSharper disable NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
        IModule guildModule;
        switch (module)
        {
            case Module.Leveling:
                guildModule = settings.LevelSettings;
                break;
            case Module.UserLog:
                guildModule = settings.UserLogSettings;
                break;
            case Module.Moderation:
                guildModule = settings.ModerationSettings;
                break;
            case Module.MessageLog:
                guildModule = settings.MessageLogSettings;
                break;
            case Module.Commands:
                guildModule = settings.CommandsSettings;
                break;
            case Module.General:
            default:
                throw new NotImplementedException();
        }

        // ReSharper enable NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
        guildModule.ModuleEnabled = enable;

        await this._settingsModule.UpdateGuildSettings(settings);

        await ctx.EditReplyAsync(message: $"{(enable ? "Enabled" : "Disabled")} {module}");

        await this._guildLog.SendLogMessageAsync(new GuildLogMessage
        {
            GuildId = ctx.Guild.Id,
            GuildLogType = GuildLogType.Moderation,
            Description = $"{ctx.User.Username} {(enable ? "Enabled" : "Disabled")} {module}",
            Color = GrimoireColor.Purple
        });
    }
}
