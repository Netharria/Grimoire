// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using System.Text.RegularExpressions;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using Grimoire.DatabaseQueryHelpers;
using Grimoire.Settings.Enums;
using JetBrains.Annotations;

namespace Grimoire.Features.CustomCommands;

public sealed partial class GetCustomCommand(IDbContextFactory<GrimoireDbContext> dbContextFactory)
{
    private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;
    private const int MaxMessageLength = 2000;
    private const int MaxEmbedDescriptionLength = 4096;

    [RequireGuild]
    [RequireModuleEnabled(Module.Commands)]
    [Command("Command")]
    [Description("Call a custom command.")]
    [UsedImplicitly]
    public async Task CallCommand(
        SlashCommandContext ctx,
        [SlashAutoCompleteProvider<GetCustomCommandOptions>]
        [Parameter("CommandName")]
        [Description("The name of the command to call.")]
        CustomCommandName name,
        [Parameter("Mention")] [Description("The person to mention if the command has one.")]
        SnowflakeObject? snowflakeObject = null,
        [Parameter("Message")] [Description("The custom message to add if the command has one.")]
        string message = "")
    {
        await ctx.DeferResponseAsync();

        if (ctx.Guild is not { } guild)
        {
            await ctx.SendWarningResponseAsync("This command can only be used in a server.");
            return;
        }

        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync();

        var response = await dbContext.CustomCommands
            .AsNoTracking()
            .GetCustomCommandQuery(guild.GetGuildId(), name)
            .FirstOrDefaultAsync();

        if (response is null
            || !IsUserAuthorized(ctx.Member, response.RestrictedUse, response.PermissionRoles))
        {
            await ctx.DeleteResponseAsync();
            return;
        }

        var content = response.Content;

        if (response.HasMention)
            content = content.Replace(
                "%Mention",
                snowflakeObject switch
                {
                    DiscordUser user => user.Mention,
                    DiscordRole { Id: var roleId } when roleId == guild.Id => "@ everyone",
                    DiscordRole role => role.Mention,
                    _ => string.Empty
                }, StringComparison.OrdinalIgnoreCase);
        if (response.HasMessage)
        {
            var sanitizedMessage = SanitizeUserMessageMentions(message, guild.Id);
            content = content.Replace("%Message", sanitizedMessage, StringComparison.OrdinalIgnoreCase);
        }
        content = TruncateForDiscord(
            content,
            response.IsEmbedded ? MaxEmbedDescriptionLength : MaxMessageLength);

        var discordResponse = new DiscordWebhookBuilder();

        if (response.IsEmbedded)
        {
            var discordEmbed = new DiscordEmbedBuilder()
                .WithDescription(content);
            if (response.EmbedColor is not null)
                discordEmbed.WithColor(GrimoireColor.FromCustomCommandEmbedColor(response.EmbedColor.Value));
            discordResponse.AddEmbed(discordEmbed);
        }
        else
            discordResponse.WithContent(content);


        await ctx.EditResponseAsync(discordResponse);
    }

    internal static string SanitizeUserMessageMentions(string input, ulong guildId)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        // Neutralize raw @everyone / @here while preserving readable text.
        var sanitized = EveryoneHereRegex().Replace(input, static match =>
            string.Concat("@ ", match.Value.AsSpan(1)));

        // Neutralize only the @everyone role mention form: <@&guildId>.
        // Keep all other role mentions intact.
        sanitized = RoleMentionRegex().Replace(sanitized, match =>
        {
            var roleIdText = match.Groups[1].Value;

            if (!ulong.TryParse(roleIdText, out var roleId) || roleId != guildId)
                return match.Value;

            // Break mention syntax but keep it readable.
            return "@ everyone";
        });

        return sanitized;
    }

    public static bool IsUserAuthorized(
        DiscordMember? member,
        bool restrictedUse,
        IReadOnlyCollection<RoleId> permissionRoles)
    {
        if (member is null)
            return false;

        if (permissionRoles.Count == 0)
            return !restrictedUse;

        var memberRoleIds = member.Roles.Select(static role => role.GetRoleId());

        var permissionsRolesSet = permissionRoles.ToFrozenSet();

        return restrictedUse
            ? memberRoleIds.Any(permissionsRolesSet.Contains)
            : memberRoleIds.All(roleId => !permissionsRolesSet.Contains(roleId));
    }

    internal static string TruncateForDiscord(string input, int maxLength)
    {
        if (string.IsNullOrEmpty(input) || input.Length <= maxLength)
            return input;

        const string ellipsis = "…";

        var truncated = input[..(maxLength - ellipsis.Length)];
        // Roll back if we split a surrogate pair
        if (truncated.Length > 0 && char.IsHighSurrogate(truncated[^1]))
            truncated = truncated[..^1];
        return string.Concat(truncated, ellipsis);
    }

    [GeneratedRegex(@"@(everyone|here)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EveryoneHereRegex();

    [GeneratedRegex("<@&(\\d+)>", RegexOptions.CultureInvariant)]
    private static partial Regex RoleMentionRegex();

}
