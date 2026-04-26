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
    internal const int MaxMessageLength = 2000;
    internal const int MaxEmbedDescriptionLength = 4096;

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
        var guild = ctx.Guild!;

        var result = await FetchAuthorizedCommandAsync(guild.GetGuildId(), name, ctx.Member);
        if (result is not Result<CustomCommandDatabaseQueryHelpers.GetCustomCommandQueryResult>.Success { Value: var response })
        {
            await ctx.DeleteResponseAsync();
            return;
        }

        await RecordUsageAsync(name, guild.GetGuildId(), ctx.User.GetUserId());
        await ctx.EditResponseAsync(BuildWebhookResponse(
            TruncateForDiscord(
                ApplyMessage(
                    ApplyMention(response.Content, response.HasMention, snowflakeObject, guild.Id),
                    response.HasMessage, message, guild.Id),
                response.OutputFormat is CommandOutputFormat.Embedded ? MaxEmbedDescriptionLength : MaxMessageLength),
            response.OutputFormat));
    }

    private async Task<Result<CustomCommandDatabaseQueryHelpers.GetCustomCommandQueryResult>> FetchAuthorizedCommandAsync(
        GuildId guildId, CustomCommandName name, DiscordMember? member)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var response = await dbContext.CustomCommands
            .AsNoTracking()
            .GetCustomCommandQuery(guildId, name)
            .FirstOrDefaultAsync();
        return response is null || !IsUserAuthorized(member, response.Access)
            ? Result<CustomCommandDatabaseQueryHelpers.GetCustomCommandQueryResult>.Fail(new Error("command.not_found", "Command not found or not authorized."))
            : Result<CustomCommandDatabaseQueryHelpers.GetCustomCommandQueryResult>.Ok(response);
    }

    private async Task RecordUsageAsync(CustomCommandName name, GuildId guildId, UserId userId)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        await dbContext.CustomCommandUsages.AddAsync(new CustomCommandUsage
        {
            Name = name, GuildId = guildId, UserId = userId, UsedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();
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
        return RoleMentionRegex().Replace(sanitized, match =>
        {
            var roleIdText = match.Groups[1].Value;
            return ulong.TryParse(roleIdText, out var roleId) && roleId == guildId
                ? "@ everyone"
                : match.Value;
        });
    }

    public static bool IsUserAuthorized(DiscordMember? member, CommandAccess access)
    {
        if (member is null) return false;
        var memberRoles = member.Roles.Select(static r => r.GetRoleId());
        return access switch
        {
            CommandAccess.Open => true,
            CommandAccess.Allowlist { Roles: { Count: 0 } } => false,
            CommandAccess.Allowlist { Roles: var roles } => memberRoles.Any(roles.ToFrozenSet().Contains),
            CommandAccess.Blocklist { Roles: var roles } => memberRoles.All(r => !roles.ToFrozenSet().Contains(r)),
            _ => false,
        };
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

    internal static DiscordWebhookBuilder BuildWebhookResponse(string content, CommandOutputFormat format)
        => format is CommandOutputFormat.Embedded { Color: var color }
            ? new DiscordWebhookBuilder().AddEmbed(BuildEmbed(content, color))
            : new DiscordWebhookBuilder().WithContent(content);

    internal static DiscordMessageBuilder BuildMessageResponse(string content, CommandOutputFormat format)
        => format is CommandOutputFormat.Embedded { Color: var color }
            ? new DiscordMessageBuilder().AddEmbed(BuildEmbed(content, color))
            : new DiscordMessageBuilder().WithContent(content);

    private static DiscordEmbedBuilder BuildEmbed(string content, CustomCommandEmbedColor? color)
        => (color is { } c
                ? new DiscordEmbedBuilder().WithColor(GrimoireColor.FromCustomCommandEmbedColor(c))
                : new DiscordEmbedBuilder())
            .WithDescription(content);

    internal static string ApplyMention(string text, bool hasMention, SnowflakeObject? snowflake, ulong guildId)
        => hasMention
            ? text.Replace("%Mention", snowflake switch
            {
                DiscordUser user => user.Mention,
                DiscordRole { Id: var roleId } when roleId == guildId => "@ everyone",
                DiscordRole role => role.Mention,
                _ => string.Empty
            }, StringComparison.OrdinalIgnoreCase)
            : text;

    internal static string ApplyMessage(string text, bool hasMessage, string message, ulong guildId)
        => hasMessage
            ? text.Replace("%Message", SanitizeUserMessageMentions(message, guildId),
                StringComparison.OrdinalIgnoreCase)
            : text;

    [GeneratedRegex(@"@(everyone|here)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EveryoneHereRegex();

    [GeneratedRegex("<@&(\\d+)>", RegexOptions.CultureInvariant)]
    private static partial Regex RoleMentionRegex();
}
