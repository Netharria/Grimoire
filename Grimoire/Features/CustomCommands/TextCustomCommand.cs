// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using Grimoire.DatabaseQueryHelpers;

namespace Grimoire.Features.CustomCommands;

public sealed class TextCustomCommand(IDbContextFactory<GrimoireDbContext> dbContextFactory)
    : IEventHandler<MessageCreatedEventArgs>
{
    private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

    public async Task HandleEventAsync(DiscordClient sender, MessageCreatedEventArgs eventArgs)
    {
        if (eventArgs.Message.MessageType is not DiscordMessageType.Default and not DiscordMessageType.Reply
            || eventArgs.Author is not DiscordMember member
            || eventArgs.Author.IsBot)
            return;
        var contentRaw = eventArgs.Message.Content;
        if (string.IsNullOrWhiteSpace(contentRaw) || !contentRaw.StartsWith('!'))
            return;

        var messageArgs = eventArgs.Message.Content[1..].Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (messageArgs.Length == 0)
            return;

        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync();

        var response = await dbContext.CustomCommands
            .AsNoTracking()
            .GetCustomCommandQuery(eventArgs.Guild.Id, messageArgs[0])
            .FirstOrDefaultAsync();

        if (response is null ||
            !GetCustomCommand.IsUserAuthorized(member, response.RestrictedUse, response.PermissionRoles))
            return;
        var content = response.Content;
        if (response.HasMention && messageArgs.Length > 1)
        {
            SnowflakeObject? snowflakeObject = null;
            if (DiscordRegex.ContainsUserMentions(messageArgs[1]))
            {
                var userIdMatches = DiscordRegex.GetUserMentions(messageArgs[1])
                    .ToArray();
                if (userIdMatches.Length > 0)
                    snowflakeObject = await sender.GetUserAsync(userIdMatches[0]);
            }
            else if (DiscordRegex.ContainsRoleMentions(messageArgs[1]))
            {
                var roleIdMatches = DiscordRegex.GetRoleMentions(messageArgs[1])
                    .ToArray();
                if (roleIdMatches.Length > 0)
                    snowflakeObject = await eventArgs.Guild.GetRoleAsync(roleIdMatches[0]);
            }


            content = content.Replace(
                "%Mention",
                snowflakeObject switch
                {
                    DiscordUser user => user.Mention,
                    DiscordRole role => role.Mention,
                    _ => string.Empty
                }, StringComparison.OrdinalIgnoreCase);
        }

        if (response.HasMessage)
            content = content.Replace("%Message",
                string.Join(' ', messageArgs
                    .Skip(response.HasMention ? 2 : 1)), StringComparison.OrdinalIgnoreCase);

        var discordResponse = new DiscordMessageBuilder();

        if (response.IsEmbedded)
        {
            var discordEmbed = new DiscordEmbedBuilder()
                .WithDescription(content);
            if (response.EmbedColor is not null)
                discordEmbed.WithColor(new DiscordColor(response.EmbedColor));
            discordResponse.AddEmbed(discordEmbed);
        }
        else
            discordResponse.WithContent(content);

        await eventArgs.Channel.SendMessageAsync(discordResponse);
    }
}
