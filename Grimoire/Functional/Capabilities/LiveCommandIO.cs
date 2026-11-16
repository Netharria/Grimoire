// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.Processors.TextCommands;
using Grimoire.Functional.Discord;
using LanguageExt;
using Unit = LanguageExt.Unit;

namespace Grimoire.Functional.Capabilities;

/// <summary>
///     The reply capability for one invocation. It holds the invoker's DM channel once opened, in an <c>Atom</c>.
/// </summary>
/// <remarks>
///     Nothing that creates a message is retried: not the initial response or the defer (past Discord's 3-second
///     window a retry can't succeed), not a follow-up or a DM (a retry could post twice) (Q25). Edits and deletes keep
///     <see cref="DiscordRetryPolicy" />. Exceptions become error cases through <see cref="DiscordErrors" /> (spec 1.4).
/// </remarks>
internal sealed class LiveCommandIO(CommandContext ctx) : ICommandIO
{
    private readonly Atom<Option<DiscordChannel>> _directMessages = Atom(Option<DiscordChannel>.None);

    public IO<Unit> Defer(Delivery delivery) =>
        this.Call("Discord.Defer", "answer this command", async () =>
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
        this.Call("Discord.Reply", "reply to this command", async () =>
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
                _ = ctx is SlashCommandContext slash
                    ? await slash.FollowupAsync(page, delivery is Delivery.Ephemeral)
                    : await ctx.FollowupAsync(page);
        });

    public IO<Unit> Refuse(Delivery delivery, DiscordEmbed embed) =>
        this.Call("Discord.Refuse", "reply to this command", async () =>
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
                    await DiscordRetryPolicy.RetryDiscordRead(async _ => await slash.DeleteResponseAsync());
                    await slash.FollowupAsync(embed, true);
                    break;
                default:
                    await this.FirstResponseAsync(embed, ephemeral: true);
                    break;
            }
        });

    public IO<Unit> React(string emoji) =>
        this.Call("Discord.React", "react to your message", async () =>
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
                await DiscordRetryPolicy.RetryDiscordRead(async _ => { await ctx.EditResponseAsync(embed); });
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

    /// <summary>
    ///     Runs one reply step through the Discord edge. A text reply the bot can't post in its channel is a refusal
    ///     with the channel, so the boundary tells the guild; an interaction reply needs no channel permission.
    /// </summary>
    private IO<Unit> Call(string name, string action, Func<Task> call) =>
        LiveDiscordIO.Attempt(this.Operation(name, action), async _ =>
            {
                await call();
                return unit;
            })
            .Bind(result => result.Match(Succ: IO.pure, Fail: IO.fail<Unit>));

    private DiscordOperation Operation(string name, string action) =>
        ctx is TextCommandContext
            ? new DiscordOperation(name, action, "this command's response",
                Optional(ctx.Guild).Map(guild => guild.GetGuildId()), ctx.GetChannelId(),
                name is "Discord.React" ? "Add Reactions" : "Send Messages")
            : new DiscordOperation(name, action, "this command's response");
}
