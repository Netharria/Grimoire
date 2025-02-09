// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Leveling.Queries;
using Grimoire.Features.Logging.UserLogging.Queries;
using Grimoire.Features.Moderation.SinAdmin;

namespace Grimoire.Features.Shared.Commands;

[SlashRequireGuild]
[SlashRequireUserGuildPermissions(DiscordPermissions.ManageGuild)]
internal sealed class UserInfoCommands(IMediator mediator) : ApplicationCommandModule
{
    private readonly IMediator _mediator = mediator;

    [SlashCommand("UserInfo", "Gets details about a user including recent Usernames and Nicknames")]
    public async Task GetUserInfo(InteractionContext ctx,
        [Option("user", "User to get the info of")]
        DiscordUser user)
    {
        await ctx.DeferAsync();

        var (color, displayName, avatarUrl, joinDate, roles) = GetUserInfo(user);


        var embed = new DiscordEmbedBuilder()
            .WithColor(color)
            .WithAuthor($"User info for {displayName}")
            .AddField("Joined On", joinDate, true)
            .WithThumbnail(avatarUrl);

        await this.GetAndAddUsernames(new GetRecentUserAndNickNames.Query { UserId = user.Id, GuildId = ctx.Guild.Id },
            embed);

        await this.GetAndAddLevelInfo(
            new GetUserLevelingInfo.Query { UserId = user.Id, GuildId = ctx.Guild.Id, RoleIds = roles }, embed,
            ctx.Guild);

        await this.GetAndAddModerationInfo(new GetUserSinCounts.Query { UserId = user.Id, GuildId = ctx.Guild.Id },
            embed);

        await ctx.EditReplyAsync(embed: embed);
    }

    private static (DiscordColor, string, string, string, ulong[]) GetUserInfo(DiscordUser user)
    {
        DiscordColor color;
        string displayName;
        string avatarUrl;
        string joinDate;
        ulong[] roles;

        if (user is DiscordMember member)
        {
            color = member.Color;
            displayName = member.DisplayName;
            avatarUrl = member.GetGuildAvatarUrl(ImageFormat.Auto);
            joinDate = Formatter.Timestamp(member.JoinedAt);
            roles = member.Roles.Select(x => x.Id).ToArray();
        }
        else
        {
            color = user.BannerColor ?? DiscordColor.Blurple;
            displayName = user.Username;
            avatarUrl = user.GetAvatarUrl(ImageFormat.Auto);
            joinDate = "Not On Server";
            roles = [];
        }

        if (string.IsNullOrEmpty(avatarUrl))
            avatarUrl = user.DefaultAvatarUrl;

        return (color, displayName, avatarUrl, joinDate, roles);
    }

    private async Task GetAndAddUsernames(GetRecentUserAndNickNames.Query query, DiscordEmbedBuilder embed)
    {
        var response = await this._mediator.Send(query);

        if (response is not null)
            embed.AddField("Usernames",
                    response.Usernames.Length == 0
                        ? "Unknown Usernames"
                        : string.Join('\n', response.Usernames),
                    true)
                .AddField("Nicknames",
                    response.Nicknames.Length == 0
                        ? "Unknown Nicknames"
                        : string.Join('\n', response.Nicknames),
                    true);
    }

    private async Task GetAndAddLevelInfo(GetUserLevelingInfo.Query query, DiscordEmbedBuilder embed,
        DiscordGuild guild)
    {
        var response = await this._mediator.Send(query);

        if (response is not null)
            embed.AddField("Level", response.Level.ToString(), true)
                .AddField("Can Gain Xp", response.IsXpIgnored ? "No" : "Yes", true)
                .AddField("Earned Rewards",
                    !response.EarnedRewards.Any()
                        ? "None"
                        : string.Join('\n', response.EarnedRewards
                            .Select(x => guild.Roles.GetValueOrDefault(x))
                            .OfType<DiscordRole>()
                            .Select(x => x.Mention)),
                    true);
    }

    private async Task GetAndAddModerationInfo(GetUserSinCounts.Query query, DiscordEmbedBuilder embed)
    {
        var response = await this._mediator.Send(query);

        if (response is not null)
            embed.AddField("Warns", response.WarnCount.ToString(), true)
                .AddField("Mutes", response.MuteCount.ToString(), true)
                .AddField("Bans", response.BanCount.ToString(), true);
    }
}
