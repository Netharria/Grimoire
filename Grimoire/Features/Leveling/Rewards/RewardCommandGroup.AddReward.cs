// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.ArgumentModifiers;
using Grimoire.Features.Shared.Channels.GuildLog;

namespace Grimoire.Features.Leveling.Rewards;

public sealed partial class RewardCommandGroup
{
    [Command("Add")]
    [Description("Adds or updates rewards for the server.")]
    public async Task AddAsync(CommandContext ctx,
        [Parameter("Role")] [Description("The role to be added as a reward.")]
        DiscordRole role,
        [Parameter("Level")] [Description("The level the reward is awarded at.")]
        int level,
        [MinMaxLength(maxLength: 4096)]
        [Parameter("Message")]
        [Description("The message to send to users when they earn a reward. Discord Markdown applies.")]
        string message = "")
    {
        await ctx.DeferResponseAsync();

        if (ctx.Guild is null)
            throw new AnticipatedException("This command can only be used in a server.");

        if (ctx.Guild.CurrentMember.Hierarchy < role.Position)
            throw new AnticipatedException($"{ctx.Guild.CurrentMember.DisplayName} will not be able to apply this " +
                                           $"reward role because the role has a higher rank than it does.");

        var guildSettings = await this._settingsModule.GetGuildSettings(ctx.Guild.Id);
        if (guildSettings is null || !guildSettings.LevelSettings.ModuleEnabled)
            throw new AnticipatedException("Leveling is not enabled on this server. Enable it with `/modules set` command.");
        var reward = guildSettings.Rewards
            .FirstOrDefault(x => x.RoleId == role.Id);

        var responseMessage = reward is not null
            ? $"Updated the reward for {role.Mention} to level {level}."
            : $"Added a new reward for {role.Mention} at level {level}.";

        reward ??= new Grimoire.Settings.Domain.Reward
        {
            GuildId = ctx.Guild.Id, RoleId = role.Id, RewardLevel = level
        };
        reward.RewardLevel = level;
        reward.RewardMessage = message;

        guildSettings.Rewards.Add(reward);

        await this._settingsModule.UpdateGuildSettings(guildSettings);

        await ctx.EditReplyAsync(GrimoireColor.DarkPurple, responseMessage);
        await this._guildLog.SendLogMessageAsync(new GuildLogMessage
        {
            GuildId = ctx.Guild.Id,
            GuildLogType = GuildLogType.Leveling,
            Color = GrimoireColor.DarkPurple,
            Description = responseMessage
        });
    }
}
