// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.


using DSharpPlus.Commands.Processors.TextCommands;
using Microsoft.Extensions.Logging;

namespace Grimoire.Extensions;

public static partial class CommandContextExtension
{
    extension(CommandContext ctx)
    {
        public ValueTask ReplyAsync(DiscordColor? color = null,
            string message = "",
            string title = "",
            string footer = "",
            DiscordEmbed? embed = null,
            DateTime? timeStamp = null,
            bool ephemeral = false)
        {
            embed ??= BuildReplyEmbed(color, message, title, footer, timeStamp);

            return ctx switch
            {
                SlashCommandContext
                    {
                        Interaction.ResponseState: DiscordInteractionResponseState.Unacknowledged
                    } slashCommandContext
                    => DiscordRetryPolicy.RetryDiscordCall(async _ =>
                        await slashCommandContext.RespondAsync(embed, ephemeral)),
                TextCommandContext { Response: null }
                    => DiscordRetryPolicy.RetryDiscordCall(async _ => await ctx.RespondAsync(embed)),
                _ => DiscordRetryPolicy.RetryDiscordCall(async _ => { await ctx.EditResponseAsync(embed); })
            };
        }

        /// <summary>Sends another message after the first reply, for answers too long to fit in one.</summary>
        public async ValueTask FollowUpAsync(DiscordColor color, string message, string title, bool ephemeral)
        {
            var embed = BuildReplyEmbed(color, message, title);
            await DiscordRetryPolicy.RetryDiscordCall(async _ =>
                ctx is SlashCommandContext slashCommandContext
                    ? await slashCommandContext.FollowupAsync(embed, ephemeral)
                    : await ctx.FollowupAsync(embed));
        }

        public ValueTask SendErrorResponseAsync(string message)
        {
            LogUserFacingError(ctx.Client.Logger, ctx.Command.FullName, ctx.Guild?.Id, message);
            return ctx.SendPrivateResponseAsync(GrimoireColor.Red, message);
        }

        public ValueTask SendWarningResponseAsync(string message)
        {
            LogUserFacingError(ctx.Client.Logger, ctx.Command.FullName, ctx.Guild?.Id, message);
            return ctx.SendPrivateResponseAsync(GrimoireColor.Yellow, message);
        }

        /// <summary>
        ///     Replies so only the invoker sees it. A deferred response keeps the visibility it was deferred with, and a
        ///     follow-up sent straight after a defer just edits that response, so the deferred response is deleted first.
        /// </summary>
        private async ValueTask SendPrivateResponseAsync(DiscordColor color, string message)
        {
            if (ctx is not SlashCommandContext
                {
                    Interaction.ResponseState: not DiscordInteractionResponseState.Unacknowledged
                } slashCommandContext)
            {
                await ctx.ReplyAsync(color, message, ephemeral: true);
                return;
            }

            await DiscordRetryPolicy.RetryDiscordCall(async _ => await slashCommandContext.DeleteResponseAsync());
            await ctx.FollowUpAsync(color, message, string.Empty, ephemeral: true);
        }

        public ValueTask SendErrorResponseAsync(IEnumerable<Error> errors)
            => ctx.SendErrorResponseAsync(string.Join("; ", errors.Select(error => error.Message)));

        [Pure]
        public Validation<DiscordChannel?> GetChannelOption(ChannelOption channelOption,
            DiscordChannel? selectedChannel)
            => channelOption switch
            {
                ChannelOption.Off => Validation<DiscordChannel?>.Succeed(null),
                ChannelOption.CurrentChannel => Validation<DiscordChannel?>.Succeed(ctx.Channel),
                ChannelOption.SelectChannel when selectedChannel is not null
                    => Validation<DiscordChannel?>.Succeed(selectedChannel),
                ChannelOption.SelectChannel => Validation<DiscordChannel?>.Fail(
                    new Error("channel.required", "Select a channel when using the Select Channel option.")),
                _ => throw new UnreachableException("Invalid ChannelOption value.")
            };

        /// <summary>
        ///     Returns the <see cref="DiscordGuild" /> for the current context wrapped in a
        ///     <see cref="Validation{T}" /> so callers are forced to handle the case where the
        ///     command is invoked outside a guild.
        /// </summary>
        /// <remarks>
        ///     Commands decorated with <c>[RequireGuild]</c> will never reach this point with a
        ///     <c>null</c> guild at runtime, but returning <see cref="Validation{T}" /> makes that
        ///     contract explicit to the compiler and prevents accidental <c>ctx.Guild!</c> suppression.
        /// </remarks>
        [Pure]
        public Validation<DiscordGuild> GetRequiredGuild()
            => ctx.Guild is { } guild
                ? Validation<DiscordGuild>.Succeed(guild)
                : Validation<DiscordGuild>.Fail(
                    new Error("guild.required", "This command can only be used in a server."));

        [Pure]
        public ModeratorId GetModeratorId() => new(ctx.User.Id);

        [Pure]
        public ChannelId GetChannelId() => new(ctx.Channel.Id);
    }

    private static DiscordEmbed BuildReplyEmbed(DiscordColor? color, string message, string title = "",
        string footer = "", DateTime? timeStamp = null)
        => new DiscordEmbedBuilder()
            .WithColor(color ?? GrimoireColor.Purple)
            .WithAuthor(title)
            .WithDescription(message)
            .WithFooter(footer)
            .WithTimestamp(timeStamp ?? DateTime.UtcNow)
            .Build();

    // These commands finish normally as far as the command handler can tell, so this is where users hitting problems shows up.
    [LoggerMessage(LogLevel.Information, "Command {Command} rejected in guild {GuildId}: {Reason}")]
    private static partial void LogUserFacingError(ILogger logger, string command, ulong? guildId, string reason);
}
