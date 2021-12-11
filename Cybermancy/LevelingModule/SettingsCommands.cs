// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using Cybermancy.Core.Enums;
using Cybermancy.Core.Features.Leveling.Commands.SetLevelSettings;
using Cybermancy.Core.Features.Leveling.Queries.GetLevelSettings;
using Cybermancy.Enums;
using Cybermancy.Extensions;
using Cybermancy.SlashCommandAttributes;
using DSharpPlus;
using DSharpPlus.SlashCommands;
using DSharpPlus.SlashCommands.Attributes;
using MediatR;

namespace Cybermancy.LevelingModule
{


    [SlashCommandGroup("LevelSettings", "Changes the settings of the Leveling Module.")]
    [SlashRequireGuild]
    [SlashRequireModuleEnabled(Module.Leveling)]
    [SlashRequirePermissions(Permissions.ManageGuild)]
    public class SettingsCommands : ApplicationCommandModule
    {
        private readonly IMediator _mediator;

        public SettingsCommands(IMediator mediator)
        {
            this._mediator = mediator;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [SlashCommand("View", "View the current settings for the leveling module.")]
        public async Task ViewAsync(InteractionContext ctx)
        {
            var response = await _mediator.Send(new GetLevelSettingsQuery{ GuildId = ctx.Guild.Id });
            var levelLogMention =
                    response.LevelChannelLog is null ?
                    "None" :
                    ctx.Guild.GetChannel(response.LevelChannelLog.Value).Mention;
            await ctx.ReplyAsync(
                title: "Current Level System settings",
                message: $"**Module Enabled:** {response.IsLevelingEnabled}\n" +
                $"**Texttime:** {response.TextTime}\n" +
                $"**Base:** {response.Base}\n" +
                $"**Modifier:** {response.Modifier}\n" +
                $"**Reward Amount:** {response.Amount}\n" +
                $"**Log-Channel:** {levelLogMention}\n");
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="levelSettings"></param>
        /// <param name="value"></param>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [SlashCommand("Set", "Set a leveling setting.")]
        public async Task SetAsync(
            InteractionContext ctx,
            [Option("Setting", "The Setting to change.")] LevelSettings levelSettings,
            [Option("Value", "The value to change the setting to. For log channel, 0 is off.")] string value)
        {
            if(levelSettings is LevelSettings.LogChannel)
            {
                var parsedValue = Regex.Match(value, @"(\d{17,21})", RegexOptions.None, TimeSpan.FromSeconds(1)).Value;
                if (ulong.TryParse(parsedValue, out var channelId))
                {
                    if (!ctx.Guild.Channels.Any(x => x.Key == channelId))
                    {
                        await ctx.ReplyAsync(CybermancyColor.Orange, message: "Did not find that channel on this server.");
                        return;
                    }
                }
                else
                {
                    await ctx.ReplyAsync(CybermancyColor.Orange, message: "Please give a valid channel." );
                    return;
                }
            }
                
            var response = await _mediator.Send(new SetLevelSettingsCommand
            {
                GuildId = ctx.Guild.Id,
                LevelSettings = levelSettings,
                Value = value
            });

            if (!response.Success)
            {
                await ctx.ReplyAsync(CybermancyColor.Orange, message: response.Message);
                return;
            }

            await ctx.ReplyAsync(message: $"Updated {levelSettings.GetName()} to {value}", ephemeral: false);
        }
    }
}
