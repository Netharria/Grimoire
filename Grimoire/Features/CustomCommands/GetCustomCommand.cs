// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using Grimoire.DatabaseQueryHelpers;
using Grimoire.Settings.Enums;
using JetBrains.Annotations;

namespace Grimoire.Features.CustomCommands;

public sealed class GetCustomCommand(IDbContextFactory<GrimoireDbContext> dbContextFactory)
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
        [Parameter("command_name")]
        [Description("The name of the command to call.")]
        CustomCommandName name,
        [Parameter("mention")] [Description("The person to mention if the command has one.")]
        SnowflakeObject? snowflakeObject = null,
        [Parameter("message")] [Description("The custom message to add if the command has one.")]
        string message = "")
    {
        await ctx.DeferResponseAsync();
        var guild = ctx.RequiredGuild;

        var result = await FetchAuthorizedCommandAsync(guild.GetGuildId(), name, ctx.Member);
        if (result is not Result<CustomCommand>.Success { Value: var response })
        {
            await ctx.DeleteResponseAsync();
            return;
        }

        if (RenderContent(response, snowflakeObject, message, guild.Id) is not { } content)
        {
            await ctx.DeleteResponseAsync();
            return;
        }

        await ctx.EditResponseAsync(BuildResponse(new DiscordWebhookBuilder(), response, content, snowflakeObject,
            guild.Id));
        // Counted once the reply is out, so a failed count can't stop the reply.
        await dbContextFactory.RecordCommandUsageAsync(name, guild.GetGuildId(), ctx.User.GetUserId());
    }

    private async Task<Result<CustomCommand>> FetchAuthorizedCommandAsync(
        GuildId guildId, CustomCommandName name, DiscordMember? member)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var response = await dbContext.CustomCommands
            .GetCustomCommandQuery(guildId, name)
            .FirstOrDefaultAsync();
        return response is null || !IsUserAuthorized(member, response)
            ? Result<CustomCommand>.Fail(new Error("command.not_found", "Command not found or not authorized."))
            : Result<CustomCommand>.Ok(response);
    }

    public static bool IsUserAuthorized(DiscordMember? member, CustomCommand command)
        => member is not null && IsUserAuthorized(
            member.Roles.Select(static role => role.GetRoleId()),
            command.Roles.OfType<CustomCommandAllowRole>().Select(role => role.RoleId),
            command.Roles.OfType<CustomCommandDenyRole>().Select(role => role.RoleId));

    /// <summary>
    ///     The policy in <c>spec-custom-command-role-policy.md</c>: no allow roles means everyone is allowed, and a deny
    ///     role always wins.
    /// </summary>
    internal static bool IsUserAuthorized(IEnumerable<RoleId> memberRoleIds, IEnumerable<RoleId> allowRoleIds,
        IEnumerable<RoleId> denyRoleIds)
    {
        var memberRoles = memberRoleIds.ToHashSet();
        var allowRoles = allowRoleIds.ToHashSet();
        return (allowRoles.Count == 0 || memberRoles.Overlaps(allowRoles)) && !memberRoles.Overlaps(denyRoleIds);
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

    /// <summary>
    ///     The reply text: <c>%Mention</c> and <c>%Message</c> filled in, cut to what Discord allows. Null when that leaves
    ///     nothing, as a command made only of the two does without them; Discord refuses an empty reply.
    /// </summary>
    internal static string? RenderContent(CustomCommand command, SnowflakeObject? mention, string message, ulong guildId)
    {
        var content = TruncateForDiscord(
            ApplyMessage(ApplyMention(command.Content.Value, mention, guildId), message),
            command is EmbedCustomCommand ? MaxEmbedDescriptionLength : MaxMessageLength);
        return string.IsNullOrWhiteSpace(content) ? null : content;
    }

    /// <summary>
    ///     Puts the rendered text in the reply: as an embed for an embed command, otherwise as plain text. Only
    ///     <c>%Mention</c>'s user or role may be pinged.
    /// </summary>
    internal static TBuilder BuildResponse<TBuilder>(TBuilder builder, CustomCommand command, string content,
        SnowflakeObject? mention, ulong guildId)
        where TBuilder : BaseDiscordMessageBuilder<TBuilder>
        => (command is EmbedCustomCommand { EmbedColor: var color }
                ? builder.AddEmbed(BuildEmbed(content, color))
                : builder.WithContent(content))
            .AddMentions(AllowedMentions(command, mention, guildId));

    /// <summary>
    ///     The one ping a reply may make: <c>%Mention</c>'s user or role. <c>@everyone</c>, <c>@here</c> and anything
    ///     typed into <c>%Message</c> are shown but never ping.
    /// </summary>
    private static IEnumerable<IMention> AllowedMentions(CustomCommand command, SnowflakeObject? mention, ulong guildId)
        => !command.Content.Value.Contains("%Mention", StringComparison.OrdinalIgnoreCase)
            ? []
            : mention switch
            {
                DiscordUser user => [new UserMention(user.Id)],
                DiscordRole role when role.Id != guildId => [new RoleMention(role.Id)],
                _ => []
            };

    private static DiscordEmbedBuilder BuildEmbed(string content, CustomCommandEmbedColor? color)
        => (color is { } c
                ? new DiscordEmbedBuilder().WithColor(GrimoireColor.FromCustomCommandEmbedColor(c))
                : new DiscordEmbedBuilder())
            .WithDescription(content);

    internal static string ApplyMention(string text, SnowflakeObject? snowflake, ulong guildId)
        => text.Replace("%Mention", snowflake switch
        {
            DiscordUser user => user.Mention,
            DiscordRole { Id: var roleId } when roleId == guildId => "@ everyone",
            DiscordRole role => role.Mention,
            _ => string.Empty
        }, StringComparison.OrdinalIgnoreCase);

    internal static string ApplyMessage(string text, string message)
        => text.Replace("%Message", message, StringComparison.OrdinalIgnoreCase);
}
