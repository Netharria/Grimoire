// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.Processors.TextCommands;
using DSharpPlus.Exceptions;
using LanguageExt;
using Unit = LanguageExt.Unit;

namespace Grimoire.Functional.Capabilities;

/// <summary>
///     The reply capability for one invocation. It holds the invoker's DM channel once opened, in an <c>Atom</c>.
/// </summary>
/// <remarks>
///     Initial responses (the defer, an undeferred refusal) aren't retried: past Discord's 3-second window a retry
///     can't succeed, and it could post twice (Q25). Edits and deletes keep <see cref="DiscordRetryPolicy" />.
///     DSharpPlus exceptions are mapped to the error cases here, as a minimal version of section 1.4's Discord edge.
/// </remarks>
internal sealed class LiveCommandIO(CommandContext ctx) : ICommandIO
{
    private readonly Atom<Option<DiscordChannel>> _directMessages = Atom(Option<DiscordChannel>.None);

    public IO<Unit> Defer(Delivery delivery) =>
        Call(async () =>
        {
            switch (ctx, delivery)
            {
                case (SlashCommandContext slash, _):
                    await slash.DeferResponseAsync(delivery is Delivery.Ephemeral);
                    break;
                case (_, Delivery.DirectMessage):
                    await this.DirectMessageChannelAsync();
                    break;
                default:
                    await ctx.DeferResponseAsync();
                    break;
            }
        });

    public IO<Unit> Reply(Delivery delivery, DiscordEmbed embed, Seq<DiscordEmbed> followUps) =>
        Call(async () =>
        {
            if (delivery is Delivery.DirectMessage)
            {
                var channel = await this.DirectMessageChannelAsync();
                foreach (var page in embed.Cons(followUps))
                    await channel.SendMessageAsync(page);
                return;
            }

            await this.FirstResponseAsync(embed, ephemeral: delivery is Delivery.Ephemeral);
            foreach (var page in followUps)
                await DiscordRetryPolicy.RetryDiscordCall(async _ => ctx is SlashCommandContext slash
                    ? await slash.FollowupAsync(page, delivery is Delivery.Ephemeral)
                    : await ctx.FollowupAsync(page));
        });

    public IO<Unit> Refuse(Delivery delivery, DiscordEmbed embed) =>
        Call(async () =>
        {
            switch (ctx, delivery)
            {
                case (_, Delivery.DirectMessage) when ctx is not SlashCommandContext:
                    await (await this.DirectMessageChannelAsync()).SendMessageAsync(embed);
                    break;
                // Deferred: a follow-up straight after a defer only edits the deferred response, which keeps the
                // defer's visibility. So the deferred response is deleted first (KI-21).
                case (SlashCommandContext
                {
                    Interaction.ResponseState: not DiscordInteractionResponseState.Unacknowledged
                } slash, _):
                    await DiscordRetryPolicy.RetryDiscordCall(async _ => await slash.DeleteResponseAsync());
                    await DiscordRetryPolicy.RetryDiscordCall(async _ => await slash.FollowupAsync(embed, true));
                    break;
                default:
                    await this.FirstResponseAsync(embed, ephemeral: true);
                    break;
            }
        });

    public IO<Unit> React(string emoji) =>
        Call(async () =>
        {
            if (ctx is TextCommandContext text)
                await text.Message.CreateReactionAsync(DiscordEmoji.FromUnicode(emoji));
        });

    /// <summary>The initial response if none was sent yet (not retried), otherwise an edit of it.</summary>
    private async Task FirstResponseAsync(DiscordEmbed embed, bool ephemeral)
    {
        switch (ctx)
        {
            case SlashCommandContext { Interaction.ResponseState: DiscordInteractionResponseState.Unacknowledged } slash:
                await slash.RespondAsync(embed, ephemeral);
                break;
            case TextCommandContext { Response: null }:
                await ctx.RespondAsync(embed);
                break;
            default:
                await DiscordRetryPolicy.RetryDiscordCall(async _ => { await ctx.EditResponseAsync(embed); });
                break;
        }
    }

    private async Task<DiscordChannel> DirectMessageChannelAsync()
    {
        if (this._directMessages.Value.Case is DiscordChannel open)
            return open;

        var member = ctx.Member
                     ?? throw new InvariantViolationException($"{ctx.Command.FullName} DMs only inside a guild.");
        var channel = await member.CreateDmChannelAsync();
        this._directMessages.Swap(_ => Some<DiscordChannel>(channel));
        return channel;
    }

    private static IO<Unit> Call(Func<Task> call) =>
        IO.liftAsync(async () =>
        {
            await call();
            return unit;
        }).MapFail(Classify);

    private static Error Classify(Error error) =>
        error.Exception.Match(
            exception => exception switch
            {
                UnauthorizedException => new Forbidden("Discord refused: the bot is missing a permission, "
                                                       + "or the member doesn't accept direct messages."),
                NotFoundException => new NotFound("The Discord interaction or message no longer exists."),
                ServerErrorException or RateLimitException or HttpRequestException
                    => Unavailable.From("Discord could not be reached.", exception),
                _ => error
            },
            () => error);
}
