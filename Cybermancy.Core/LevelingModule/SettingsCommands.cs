using Cybermancy.Core.Contracts.Services;
using Cybermancy.Core.Extensions;
using DSharpPlus;
using DSharpPlus.SlashCommands;
using DSharpPlus.SlashCommands.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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
        LogChannel
    }
    [SlashCommandGroup("LevelSettings", "Changes the settings of the Leveling Module.")]
    [SlashRequireGuild]
    [SlashRequirePermissions(Permissions.ManageGuild)]
    public class SettingsCommands : ApplicationCommandModule
    {
        public ILevelSettingsService _levelSettingsService;
        public SettingsCommands(ILevelSettingsService levelSettingsService)
        {
            _levelSettingsService = levelSettingsService;
        }

        [SlashCommand("View", "View the current settings for the leveling module.")]
        public async Task View(InteractionContext ctx)
        {
            var guild = await _levelSettingsService.GetGuild(ctx.Guild.Id);
            var levelLogMention =
                    guild.LevelChannelLog is null ?
                    "None" :
                    ctx.Guild.GetChannel(guild.LevelChannelLog.Value).Mention;
            await ctx.Reply(title: "Current Level System settings",
               message: $"**Texttime:** {guild.TextTime}\n" +
                        $"**Base:** {guild.Base}\n" +
                        $"**Modifier:** {guild.Modifier}\n" +
                        $"**Reward Amount:** {guild.Amount}\n" +
                        $"**Log-Channel:** {levelLogMention}\n");
        }

        [SlashCommand("Set", "Set a leveling setting.")]
        public async Task Set(InteractionContext ctx, 
            [Option("Setting", "The Setting to change.")]LevelSettings levelSettings,
            [Option("Value", "The value to change the setting to. For log channel, 0 is off.")]string value)
        {
            var guild = await _levelSettingsService.GetGuild(ctx.Guild.Id);
            switch (levelSettings)
            {
                case LevelSettings.TextTime:
                    if (int.TryParse(value, out var textTime))
                        guild.TextTime = textTime;
                    else
                    {
                        await ctx.Reply(message: "Please give a valid number for TextTime.");
                        return;
                    }
                    break;
                case LevelSettings.Base:
                    if (int.TryParse(value, out var baseXp))
                        guild.Base = baseXp;
                    else
                    {
                        await ctx.Reply(message: "Please give a valid number for base XP.");
                        return;
                    }
                    break;
                case LevelSettings.Modifier:
                    if (int.TryParse(value, out var modifier))
                        guild.Modifier = modifier;
                    else 
                    { 
                        await ctx .Reply(message: "Please give a valid number for Modifier.");
                        return;
                    }
                    break;
                case LevelSettings.Amount:
                    if (int.TryParse(value, out var amout))
                        guild.Amount = amout;
                    else 
                    { 
                        await ctx.Reply(message: "Please give a valid number for Amount.");
                        return;
                    }
                    break;
                case LevelSettings.LogChannel:
                    string parsedValue = Regex.Match(value, @"(\d{18})").Value;
                    if (ulong.TryParse(parsedValue, out var channelId))
                    {
                        if(ctx.Guild.Channels.Any(x => x.Key == channelId))
                        {
                            guild.LevelChannelLog = channelId;
                        }
                        else 
                        { 
                            await ctx .Reply(message: "Did not find that channel on this server.");
                            return;
                        }
                    }
                    else 
                    {
                        await ctx .Reply(message: "Please give a valid channel.");
                        return;
                    }
                    break;
            }
            await _levelSettingsService.Update(guild);
            await ctx.Reply(message: $"Updated {levelSettings.GetName()} to {value}", ephemeral: false);
        }
    }
}
