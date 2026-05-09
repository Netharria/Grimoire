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
    public async Task HandleEventAsync(DiscordClient sender, MessageCreatedEventArgs eventArgs)
    {
        if (eventArgs.Message.MessageType is not DiscordMessageType.Default and not DiscordMessageType.Reply
            || eventArgs.Author is not DiscordMember member
            || eventArgs.Author.IsBot)
            return;

        var contentRaw = eventArgs.Message.Content;
        if (string.IsNullOrWhiteSpace(contentRaw) || !contentRaw.StartsWith('!'))
            return;

        var messageArgs = contentRaw[1..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (messageArgs.Length == 0)
            return;

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();

        var commandName = CustomCommandName.ParseFromDatabase(messageArgs[0]);
        var response = await dbContext.CustomCommands
            .AsNoTracking()
            .GetCustomCommandQuery(member.GetGuildId(), commandName)
            .FirstOrDefaultAsync();

        if (response is null ||
            !GetCustomCommand.IsUserAuthorized(member, response.Access))
            return;

        await dbContext.CustomCommandUsages.AddAsync(new CustomCommandUsage
        {
            Name = commandName,
            GuildId = member.GetGuildId(),
            UserId = member.GetUserId(),
            UsedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var snowflakeObject = response.HasMention && messageArgs.Length > 1
            ? await ResolveSnowflake(messageArgs[1])
            : null;

        var rawMessage = response.HasMessage
            ? string.Join(' ', messageArgs.Skip(response.HasMention ? 2 : 1))
            : string.Empty;

        var content = GetCustomCommand.TruncateForDiscord(
            GetCustomCommand.ApplyMessage(
                GetCustomCommand.ApplyMention(response.Content, response.HasMention, snowflakeObject, member.Guild.Id),
                response.HasMessage, rawMessage, member.Guild.Id),
            response.OutputFormat is CommandOutputFormat.Embedded
                ? GetCustomCommand.MaxEmbedDescriptionLength
                : GetCustomCommand.MaxMessageLength);

        await eventArgs.Channel.SendMessageAsync(
            GetCustomCommand.BuildMessageResponse(content, response.OutputFormat));
        return;

        async Task<SnowflakeObject?> ResolveSnowflake(string arg)
        {
            var userIds = DiscordRegex.GetUserMentions(arg).ToArray();
            if (userIds.Length > 0) return await sender.GetUserOrDefaultAsync(new UserId(userIds[0]));
            var roleIds = DiscordRegex.GetRoleMentions(arg).ToArray();
            if (roleIds.Length > 0) return await eventArgs.Guild.GetRoleOrDefaultAsync(new RoleId(roleIds[0]));
            return null;
        }
    }
}
