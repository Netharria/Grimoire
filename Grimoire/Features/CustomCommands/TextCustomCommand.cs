// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

namespace Grimoire.Features.CustomCommands;

public sealed class TextCustomCommand
{
    public sealed class EventHandler(IMediator mediator) : IEventHandler<MessageCreatedEventArgs>
    {
        private readonly IMediator _mediator = mediator;

        public async Task HandleEventAsync(DiscordClient sender, MessageCreatedEventArgs eventArgs)
        {
            if (eventArgs.Message.MessageType is not DiscordMessageType.Default and not DiscordMessageType.Reply
                || eventArgs.Author is not DiscordMember member
                || eventArgs.Author.IsBot)
                return;
            if (!eventArgs.Message.Content.StartsWith('!'))
                return;

            var messageArgs = eventArgs.Message.Content[1..].Split(' ');

            if (messageArgs.Length == 0)
                return;

            var response =
                await this._mediator.Send(new GetCustomCommand.Request
                {
                    Name = messageArgs[0], GuildId = eventArgs.Guild.Id
                });

            if (response is null || !GetCustomCommand.IsUserAuthorized(member, response))
                return;
            if (response.HasMention && messageArgs.Length > 1)
            {
                SnowflakeObject? snowflakeObject = null;
                if(DiscordRegex.ContainsUserMentions(messageArgs[1]))
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


                response.Content = response.Content.Replace(
                    "%Mention",
                    snowflakeObject switch
                    {
                        DiscordUser user => user.Mention,
                        DiscordRole role => role.Mention,
                        _ => string.Empty
                    }, StringComparison.OrdinalIgnoreCase);
            }
            if (response.HasMessage)
                response.Content = response.Content.Replace("%Message",
                    string.Join(' ', messageArgs
                        .Skip(response.HasMention ? 2 : 1)), StringComparison.OrdinalIgnoreCase);

            var discordResponse = new DiscordMessageBuilder();

            if (response.IsEmbedded)
            {
                var discordEmbed = new DiscordEmbedBuilder()
                    .WithDescription(response.Content);
                if (response.EmbedColor is not null)
                    discordEmbed.WithColor(new DiscordColor(response.EmbedColor));
                discordResponse.AddEmbed(discordEmbed);
            }
            else
                discordResponse.WithContent(response.Content);

            await eventArgs.Channel.SendMessageAsync(discordResponse);
        }
    }
}
