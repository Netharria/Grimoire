// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.ArgumentModifiers;
using DSharpPlus.Commands.ContextChecks;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Domain.Documents;
using Grimoire.Settings.Domain.Values;
using Grimoire.Settings.Enums;
using JetBrains.Annotations;
using LanguageExt;

namespace Grimoire.Features.Leveling.Rewards;

[UsedImplicitly]
public sealed partial class RewardCommandGroup
{
    [UsedImplicitly]
    [RequireGuild]
    [RequireModuleEnabled(Module.Leveling)]
    [RequireUserGuildPermissions(DiscordPermission.ManageGuild)]
    [Command("Add")]
    [Description("Adds or updates rewards for the server.")]
    public async Task AddAsync(CommandContext ctx,
        [Parameter("role")] [Description("The role to be added as a reward.")]
        DiscordRole role,
        [MinMaxValue(RewardEntry.MinimumLevel, int.MaxValue)]
        [Parameter("level")]
        [Description("The level the reward is awarded at.")]
        int level,
        [MinMaxLength(maxLength: 4096)]
        [Parameter("message")]
        [Description("The message to send to users when they earn a reward. Discord Markdown applies.")]
        string? message = null)
    {
        await ctx.DeferResponseAsync();
        var guild = ctx.RequiredGuild;
        await ValidateBotCanGrant(guild, role)
            .Bind(_ => RewardMessage.CreateIfNotNull(message))
            .Bind(validatedMessage => RewardEntry.Create(role.GetRoleId(), level, validatedMessage.ToNullable()))
            .ToFin()
            .BindAsync(reward => this._settingsModule.SetRewardAsync(guild.GetGuildId(), ctx.GetModeratorId(), reward))
            .Match(
                write => this.OnAddedAsync(ctx, guild,
                    DescribeAdd(write, role.Mention, ctx.User.Mention, level)),
                error => OnFail(ctx, error));
    }

    private static Validation<Error, DiscordRole> ValidateBotCanGrant(DiscordGuild guild, DiscordRole role)
        => role switch
        {
            { IsManaged: true } => new Invalid("That role is managed by an integration, such as a bot or Server Booster, so it can't be given out."),
            // Discord needs the bot's top role strictly above the role it grants.
            _ when role.Position >= guild.CurrentMember.Hierarchy => new Invalid($"{guild.CurrentMember.DisplayName} will not be able to apply this " +
                $"reward role because the role has the same or a higher rank than it does."),
            _ => Success<Error, DiscordRole>(role)
        };

    private static Task OnFail(CommandContext ctx, Error error)
        => ctx.ReplyAsync(GrimoireColor.Red,
                $"Was not able to update the rewards for the server:\n{error.Message}")
            .AsTask();

    /// <summary>The reply to <c>/Rewards Add</c>, and the mod log line when something changed.</summary>
    internal sealed record AddReply(DiscordColor Color, string Reply, string? Log);

    /// <summary>
    ///     Words what <c>/Rewards Add</c> did (spec 2.5, UX change 1). Rewards are only ever granted, so members who
    ///     already have a moved reward keep it, and the reply says so.
    /// </summary>
    internal static AddReply DescribeAdd(Write<RewardChange> write, string role, string moderator, int level)
    {
        return write.Match(
            saved: change => change switch
            {
                RewardChange.Added => new AddReply(GrimoireColor.Green,
                    $"{role} is now a reward at level {level}.",
                    $"{moderator} added {role} as a reward at level {level}."),
                RewardChange.Moved moved => new AddReply(GrimoireColor.Green,
                    $"{role} moved from level {moved.FromLevel} to level {level}. Members who already have it keep it.",
                    $"{moderator} moved the {role} reward from level {moved.FromLevel} to level {level}."),
                RewardChange.MessageChanged => new AddReply(GrimoireColor.Green,
                    $"Updated the message for the {role} reward (level {level}).",
                    $"{moderator} updated the message for the {role} reward (level {level})."),
                _ => Unchanged()
            },
            unchanged: _ => Unchanged());

        AddReply Unchanged() => new(GrimoireColor.Yellow, $"{role} is already a reward at level {level}.", null);
    }

    private async Task OnAddedAsync(CommandContext ctx, DiscordGuild guild, AddReply reply)
    {
        await ctx.ReplyAsync(reply.Color, reply.Reply);
        if (reply.Log is { } log)
            await this._guildLog.SendLogMessageAsync(new GuildLogTextMessage
            {
                GuildId = guild.GetGuildId(),
                GuildLogType = GuildLogType.Moderation,
                Color = GrimoireColor.DarkPurple,
                Description = log
            });
    }
}
