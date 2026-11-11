// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using System.Linq.Expressions;

namespace Grimoire.DatabaseQueryHelpers;

public static class CustomCommandDatabaseQueryHelpers
{
    /// <summary>The current version of each command: the rows with no newer version of the same name.</summary>
    public static IQueryable<CustomCommand> LatestVersions(this IQueryable<CustomCommand> customCommands)
        => customCommands.Where(command => !customCommands.Any(y =>
            y.GuildId == command.GuildId && y.Name == command.Name && y.CreatedAt > command.CreatedAt));

    /// <summary>The current version of one command, with its roles, untracked.</summary>
    public static IQueryable<CustomCommand> GetCustomCommandQuery(
        this IQueryable<CustomCommand> customCommands, GuildId guildId, CustomCommandName commandName)
        => customCommands
            .LatestVersions()
            .AsNoTracking()
            .Include(command => command.Roles)
            .Where(command => command.GuildId == guildId && command.Name == commandName);

    /// <summary>
    ///     The commands <paramref name="member" /> may know about (KI-86): all of them for a member with Manage Guild, who
    ///     manages them, otherwise only those the member may run. Apply it to <see cref="LatestVersions" />, since the
    ///     newest version's roles are the ones that apply.
    /// </summary>
    public static IQueryable<CustomCommand> VisibleTo(this IQueryable<CustomCommand> customCommands,
        DiscordMember member)
        => ManagesCommands(member)
            ? customCommands
            : customCommands.Where(RunnableBy(MemberRoleIds(member)));

    /// <summary>The opposite of <see cref="VisibleTo" />: the commands to leave out of what the member is shown.</summary>
    public static IQueryable<CustomCommand> HiddenFrom(this IQueryable<CustomCommand> customCommands,
        DiscordMember member)
        => ManagesCommands(member)
            ? customCommands.Where(_ => false)
            : customCommands.Where(Negate(RunnableBy(MemberRoleIds(member))));

    private static bool ManagesCommands(DiscordMember member)
        => member.Permissions.HasPermission(DiscordPermission.ManageGuild);

    private static List<RoleId> MemberRoleIds(DiscordMember member)
        => [.. member.Roles.Select(static role => role.GetRoleId())];

    /// <summary>
    ///     The SQL form of <c>GetCustomCommand.IsUserAuthorized</c>, so the policy in
    ///     <c>spec-custom-command-role-policy.md</c> can filter before paging: no allow roles means everyone may, and a
    ///     deny role always wins.
    /// </summary>
    private static Expression<Func<CustomCommand, bool>> RunnableBy(IReadOnlyList<RoleId> memberRoleIds)
        => command =>
            !command.Roles.Any(role => role is CustomCommandDenyRole && memberRoleIds.Contains(role.RoleId))
            && (!command.Roles.Any(role => role is CustomCommandAllowRole)
                || command.Roles.Any(role => role is CustomCommandAllowRole && memberRoleIds.Contains(role.RoleId)));

    private static Expression<Func<T, bool>> Negate<T>(Expression<Func<T, bool>> predicate)
        => Expression.Lambda<Func<T, bool>>(Expression.Not(predicate.Body), predicate.Parameters);

    public static async Task RecordCommandUsageAsync(
        this IDbContextFactory<GrimoireDbContext> dbContextFactory,
        CustomCommandName name,
        GuildId guildId,
        UserId userId)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        await dbContext.CustomCommandUsages.AddAsync(new CustomCommandUsage
        {
            Name = name, GuildId = guildId, UserId = userId, UsedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();
    }
}
