// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using Grimoire.Domain.Shared;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Features.Shared.Settings;
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

        await this._mediator.Send(new EnableModule.Request
        {
            GuildId = ctx.Guild.Id, Module = module, Enable = enable
        });

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

internal sealed class EnableModule
{
    public sealed record Request : IRequest
    {
        public GuildId GuildId { get; init; }
        public Module Module { get; init; }
        public bool Enable { get; init; }
    }

    public sealed class Handler(SettingsModule settingsModule)
        : IRequestHandler<Request>
    {
        private readonly SettingsModule _settingsModule = settingsModule;

        public async Task Handle(Request command,
            CancellationToken cancellationToken)
        {
            var settings = await this._settingsModule.GetGuildSettings(command.GuildId, cancellationToken);

            if (settings is null)
                throw new AnticipatedException("Guild settings not found.");

            // ReSharper disable NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
            IModule guildModule;
            switch (command.Module)
            {
                case Module.Leveling:
                    settings.LevelSettings ??= new GuildLevelSettings { GuildId = command.GuildId };
                    guildModule = settings.LevelSettings;
                    break;
                case Module.UserLog:
                    settings.UserLogSettings ??= new GuildUserLogSettings { GuildId = command.GuildId };
                    guildModule = settings.UserLogSettings;
                    break;
                case Module.Moderation:
                    settings.ModerationSettings ??= new GuildModerationSettings { GuildId = command.GuildId };
                    guildModule = settings.ModerationSettings;
                    break;
                case Module.MessageLog:
                    settings.MessageLogSettings ??= new GuildMessageLogSettings { GuildId = command.GuildId };
                    guildModule = settings.MessageLogSettings;
                    break;
                case Module.Commands:
                    settings.CommandsSettings ??= new GuildCommandsSettings { GuildId = command.GuildId };
                    guildModule = settings.CommandsSettings;
                    break;
                case Module.General:
                default:
                    throw new NotImplementedException();
            }

            // ReSharper enable NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
            guildModule.ModuleEnabled = command.Enable;

            await this._settingsModule.UpdateGuildSettings(settings, cancellationToken);
        }
    }
}
