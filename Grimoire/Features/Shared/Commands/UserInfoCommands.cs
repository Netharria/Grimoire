// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.ContextChecks;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;

namespace Grimoire.Features.Shared.Commands;

[RequireGuild]
[RequireUserGuildPermissions(DiscordPermission.ManageGuild)]
internal sealed class UserInfoCommands(
    IDbContextFactory<GrimoireDbContext> dbContextFactory,
    SettingsModule settingsModule)
{
    private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;
    private readonly SettingsModule _settingsModule = settingsModule;

    [Command("UserInfo")]
    [Description("Get information about a user.")]
    public async Task GetUserInfo(CommandContext ctx,
        [Parameter("user")] [Description("The user to get the information of.")]
        DiscordUser user)
    {
        await ctx.DeferResponseAsync();

        var guild = ctx.Guild!;

        var (color, displayName, avatarUrl, joinDate, roles) = GetUserInfo(user);


        var embed = new DiscordEmbedBuilder()
            .WithColor(color)
            .WithAuthor($"User info for {displayName}")
            .AddField("Joined On", joinDate, true)
            .WithThumbnail(avatarUrl);

        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync();

        await GetAndAddUsernames(dbContext, user.GetUserId(), guild.GetGuildId(), embed);

        await GetAndAddLevelInfo(dbContext, embed, user.GetUserId(), guild.GetGuildId(),
            roles.Select(x => new RoleId(x)).ToHashSet());

        await GetAndAddModerationInfo(dbContext, guild.GetGuildId(), user.GetUserId(), embed);

        await ctx.ReplyAsync(embed: embed);
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
            color = member.Color.PrimaryColor;
            displayName = member.DisplayName;
            avatarUrl = member.GetGuildAvatarUrl(MediaFormat.Auto);
            joinDate = Formatter.Timestamp(member.JoinedAt);
            roles = member.Roles.Select(x => x.Id).ToArray();
        }
        else
        {
            color = user.BannerColor ?? DiscordColor.Blurple;
            displayName = user.Username;
            avatarUrl = user.GetAvatarUrl(MediaFormat.Auto);
            joinDate = "Not On Server";
            roles = [];
        }

        if (string.IsNullOrEmpty(avatarUrl))
            avatarUrl = user.DefaultAvatarUrl;

        return (color, displayName, avatarUrl, joinDate, roles);
    }

    private async Task GetAndAddUsernames(
        GrimoireDbContext dbContext,
        UserId userId,
        GuildId guildId,
        DiscordEmbedBuilder embed)
    {
        if (!await this._settingsModule.IsModuleEnabled(Module.UserLog, guildId)
                .GetOrElse(() => false))
            return;
        var usernames = await dbContext.UsernameHistory
            .AsNoTracking()
            .Where(usernameHistory => usernameHistory.UserId == userId)
            .OrderByDescending(history => history.Timestamp)
            .Take(3)
            .Select(usernameHistory => usernameHistory.Username)
            .ToArrayAsync();

        var nicknames = await dbContext.NicknameHistory
            .Where(nicknameHistory => nicknameHistory.UserId == userId
                                      && nicknameHistory.GuildId == guildId)
            .Where(nicknameHistory => nicknameHistory.Nickname != null)
            .OrderByDescending(nicknameHistory => nicknameHistory.Timestamp)
            .Take(3)
            .Select(nicknameHistory => nicknameHistory.Nickname)
            .OfType<string>()
            .ToArrayAsync();

        embed.AddField("Usernames",
                usernames.Length == 0
                    ? "Unknown Usernames"
                    : string.Join('\n', usernames),
                true)
            .AddField("Nicknames",
                nicknames.Length == 0
                    ? "Unknown Nicknames"
                    : string.Join('\n', nicknames),
                true);
    }

    private async Task GetAndAddLevelInfo(
        GrimoireDbContext dbContext,
        DiscordEmbedBuilder embed,
        UserId userId,
        GuildId guildId,
        IReadOnlySet<RoleId> roleIds)
    {
        if (!await this._settingsModule.IsModuleEnabled(Module.Leveling, guildId)
                .GetOrElse(() => false))
            return;

        var membersXp = await dbContext.XpHistory
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.GuildId == guildId)
            .SumAsync(x => x.RawXp);

        var levelSettings = await this._settingsModule.GetLevelingSettings(guildId)
            .GetOrElse(() => default!);

        var rewards = await this._settingsModule.GetLevelingRewardsAsync(guildId)
            .GetOrElse(() => default!);


        var membersLevel = levelSettings.GetLevelFromXp(membersXp);
        var earnedRewards = rewards
            .Where(x => x.RewardLevel <= membersLevel)
            .Select(x => x.RoleId)
            .ToArray();

        var isXpIgnored = await this._settingsModule.IsMemberIgnored(guildId, userId, roleIds)
            .GetOrElse(() => false);

        embed.AddField("Level", membersLevel.ToString(), true)
            .AddField("Can Gain Xp", isXpIgnored ? "No" : "Yes", true)
            .AddField("Earned Rewards",
                earnedRewards.Length > 0
                    ? "None"
                    : string.Join('\n', earnedRewards.Select(x => $"<@&{x}>")),
                true);
    }

    private async Task GetAndAddModerationInfo(
        GrimoireDbContext dbContext,
        GuildId guildId,
        UserId userId,
        DiscordEmbedBuilder embed)
    {
        if (!await this._settingsModule.IsModuleEnabled(Module.Moderation, guildId)
                .GetOrElse(() => false))
            return;
        var autoPardonAfter = await this._settingsModule.GetAutoPardonDuration(guildId)
            .GetOrElse(() => TimeSpan.FromDays(10950));
        var response = await dbContext.Sins
            .AsNoTracking()
            .Where(sin => sin.UserId == userId
                          && sin.GuildId == guildId
                          && sin.SinOn > DateTimeOffset.UtcNow - autoPardonAfter)
            .GroupBy(sin => new { sin.UserId, sin.GuildId, sin.SinType })
            .ToDictionaryAsync(
                group => group.Key.SinType,
                group => group.Count());

        embed.AddField("Warns", response.GetValueOrDefault(SinType.Warn, 0).ToString(), true)
            .AddField("Mutes", response.GetValueOrDefault(SinType.Mute, 0).ToString(), true)
            .AddField("Bans", response.GetValueOrDefault(SinType.Ban, 0).ToString(), true);
    }
}
