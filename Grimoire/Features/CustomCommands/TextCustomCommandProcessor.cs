// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using Grimoire.DatabaseQueryHelpers;

namespace Grimoire.Features.CustomCommands;

public sealed class TextCustomCommandProcessor(IDbContextFactory<GrimoireDbContext> dbContextFactory)
    : IEventHandler<MessageCreatedEventArgs>
{
    public async Task HandleEventAsync(DiscordClient sender, MessageCreatedEventArgs eventArgs) =>
        await Validation<MessageCreatedEventArgs>.Succeed(eventArgs)
            .Bind(ev => ev switch
            {
                {
                        Message.MessageType: DiscordMessageType.Default or DiscordMessageType.Reply,
                        Author: DiscordMember { IsBot: false } member
                    } =>
                    Validation<(DiscordMember Member, string Content)>.Succeed((member, ev.Message.Content)),
                _ => Validation<(DiscordMember Member, string Content)>.Fail(
                    new Error("text-command.invalid-event", "Not a valid command event"))
            })
            .Bind(x => !string.IsNullOrWhiteSpace(x.Content) && x.Content.StartsWith('!')
                                                             && x.Content[1..].Split(' ',
                                                                     StringSplitOptions.RemoveEmptyEntries) is
                                                                 { Length: > 0 } parsedArgs
                ? Validation<(DiscordMember Member, string[] Args)>.Succeed((x.Member, parsedArgs))
                : Validation<(DiscordMember Member, string[] Args)>.Fail(
                    new Error("text-command.not-a-command", "Message is not a command")))
            .Bind(x => CustomCommandName.Create(x.Args[0]).Map(name => (x.Member, Name: name, x.Args)))
            .ToResult()
            .BindAsync(x => FetchCommandAsync(x.Member, x.Name, x.Args))
            .TapAsync(cmd => RecordUsageAsync(cmd.Name, cmd.Member.GetGuildId(), cmd.Member.GetUserId()))
            .TapAsync(async cmd =>
            {
                var snowflake = cmd.Response.Content.Value.Contains("%Mention", StringComparison.OrdinalIgnoreCase) &&
                                cmd.Args.Length > 1
                    ? await ResolveSnowflake(cmd.Args[1], sender, eventArgs.Guild)
                    : null;
                await eventArgs.Channel.SendMessageAsync(
                    BuildResponse(cmd.Response, cmd.Args, snowflake, eventArgs.Guild.Id));
            });

    private async Task<Result<ParsedCommand>> FetchCommandAsync(DiscordMember member, CustomCommandName name,
        string[] args)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var response = await dbContext.CustomCommands
            .AsNoTracking()
            .GetCustomCommandQuery(member.GetGuildId(), name)
            .FirstOrDefaultAsync();
        return response is null || !GetCustomCommand.IsUserAuthorized(member, response)
            ? Result<ParsedCommand>.Fail(new Error("text-command.not-found", "Command not found or not authorized"))
            : Result<ParsedCommand>.Ok(new ParsedCommand(response, member, name, args));
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

    private static DiscordMessageBuilder BuildResponse(
        CustomCommand response,
        string[] args, SnowflakeObject? snowflake, ulong guildId)
    {
        var contentValue = response.Content.Value;
        var hasMention = contentValue.Contains("%Mention", StringComparison.OrdinalIgnoreCase);
        var rawMessage = string.Join(' ', args.Skip(hasMention ? 2 : 1));
        CommandOutputFormat outputFormat = response is EmbedCustomCommand embed
            ? new CommandOutputFormat.Embedded(embed.EmbedColor)
            : new CommandOutputFormat.Text();
        var content = GetCustomCommand.TruncateForDiscord(
            GetCustomCommand.ApplyMessage(
                GetCustomCommand.ApplyMention(contentValue, snowflake, guildId),
                rawMessage, guildId),
            outputFormat is CommandOutputFormat.Embedded
                ? GetCustomCommand.MaxEmbedDescriptionLength
                : GetCustomCommand.MaxMessageLength);
        return GetCustomCommand.BuildMessageResponse(content, outputFormat);
    }

    private static async Task<SnowflakeObject?> ResolveSnowflake(string arg, DiscordClient sender, DiscordGuild guild)
    {
        var userIds = DiscordRegex.GetUserMentions(arg).ToArray();
        if (userIds.Length > 0) return await sender.GetUserOrDefaultAsync(new UserId(userIds[0]));
        var roleIds = DiscordRegex.GetRoleMentions(arg).ToArray();
        if (roleIds.Length > 0) return await guild.GetRoleOrDefaultAsync(new RoleId(roleIds[0]));
        return null;
    }

    private sealed record ParsedCommand(
        CustomCommand Response,
        DiscordMember Member,
        CustomCommandName Name,
        string[] Args);
}
