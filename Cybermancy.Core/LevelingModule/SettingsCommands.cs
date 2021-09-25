// -----------------------------------------------------------------------
// <copyright file="SettingsCommands.cs" company="Netharia">
// Copyright (c) Netharia. All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Cybermancy.Core.Contracts.Services;
using Cybermancy.Core.Extensions;
using DSharpPlus;
using DSharpPlus.SlashCommands;
using DSharpPlus.SlashCommands.Attributes;

namespace Cybermancy.Core.LevelingModule
{
    public enum LevelSettings
    {
        [ChoiceName("TextTime")]
        TextTime,
        [ChoiceName("Base")]
        Base,
        [ChoiceName("Modifier")]
        Modifier,
        [ChoiceName("Amount")]
        Amount,
        [ChoiceName("LogChannel")]
        LogChannel,
    }

    [SlashCommandGroup("LevelSettings", "Changes the settings of the Leveling Module.")]
    [SlashRequireGuild]
    [SlashRequirePermissions(Permissions.ManageGuild)]
    public class SettingsCommands : ApplicationCommandModule
    {
        private readonly ILevelSettingsService _levelSettingsService;

        /// <summary>
        /// Initializes a new instance of the <see cref="SettingsCommands"/> class.
        /// </summary>
        /// <param name="levelSettingsService"></param>
        public SettingsCommands(ILevelSettingsService levelSettingsService)
        {
            this._levelSettingsService = levelSettingsService;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [SlashCommand("View", "View the current settings for the leveling module.")]
        public async Task ViewAsync(InteractionContext ctx)
        {
            var guild = await this._levelSettingsService.GetGuildAsync(ctx.Guild.Id);
            var levelLogMention =
                    guild.LevelChannelLog is null ?
                    "None" :
                    ctx.Guild.GetChannel(guild.LevelChannelLog.Value).Mention;
            await ctx.ReplyAsync(
                title: "Current Level System settings",
                message: $"**Texttime:** {guild.TextTime}\n**Base:** {guild.Base}\n**Modifier:** {guild.Modifier}\n**Reward Amount:** {guild.Amount}\n**Log-Channel:** {levelLogMention}\n");
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
            var guild = await this._levelSettingsService.GetGuildAsync(ctx.Guild.Id);
            switch (levelSettings)
            {
                case LevelSettings.TextTime:
                    if (int.TryParse(value, NumberStyles.Number, new CultureInfo("en-us"), out var textTime))
                    {
                        guild.TextTime = textTime;
                    }
                    else
                    {
                        await ctx.ReplyAsync(message: "Please give a valid number for TextTime.");
                        return;
                    }

                    break;
                case LevelSettings.Base:
                    if (int.TryParse(value, NumberStyles.Number, new CultureInfo("en-us"), out var baseXp))
                    {
                        guild.Base = baseXp;
                    }
                    else
                    {
                        await ctx.ReplyAsync(message: "Please give a valid number for base XP.");
                        return;
                    }

                    break;
                case LevelSettings.Modifier:
                    if (int.TryParse(value, NumberStyles.Number, new CultureInfo("en-us"), out var modifier))
                    {
                        guild.Modifier = modifier;
                    }
                    else
                    {
                        await ctx.ReplyAsync(message: "Please give a valid number for Modifier.");
                        return;
                    }

                    break;
                case LevelSettings.Amount:
                    if (int.TryParse(value, NumberStyles.Number, new CultureInfo("en-us"), out var amout))
                    {
                        guild.Amount = amout;
                    }
                    else
                    {
                        await ctx.ReplyAsync(message: "Please give a valid number for Amount.");
                        return;
                    }

                    break;
                case LevelSettings.LogChannel:
                    var parsedValue = Regex.Match(value, @"(\d{18})", RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1)).Value;
                    if (ulong.TryParse(parsedValue, NumberStyles.Number, new CultureInfo("en-us"), out var channelId))
                    {
                        if (ctx.Guild.Channels.Any(x => x.Key == channelId))
                        {
                            guild.LevelChannelLog = channelId;
                        }
                        else
                        {
                            await ctx.ReplyAsync(message: "Did not find that channel on this server.");
                            return;
                        }
                    }
                    else
                    {
                        await ctx.ReplyAsync(message: "Please give a valid channel.");
                        return;
                    }

                    break;
            }

            await this._levelSettingsService.UpdateAsync(guild);
            await ctx.ReplyAsync(message: $"Updated {levelSettings.GetName()} to {value}", ephemeral: false);
        }
    }
}
