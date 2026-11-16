// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Functional.Capabilities;
using LanguageExt;
using Microsoft.Extensions.DependencyInjection;
using Unit = LanguageExt.Unit;

namespace Grimoire.Functional.Discord;

/// <summary>
///     The live Discord capability (spec 1.4). It resolves ids with DSharpPlus's own lookups, which return the cached
///     entity when DSharpPlus has one and ask REST when it doesn't; Grimoire keeps no cache of its own. Every call runs
///     through <see cref="Attempt{A}" />, the one place its exceptions become values (<see cref="DiscordErrors" />).
/// </summary>
/// <remarks>
///     <c>DiscordClient</c> is resolved on first use, as <c>LiveAlertIO</c> does: it depends on the error handler, so
///     resolving it at construction would be a cycle.
/// </remarks>
internal sealed class LiveDiscordIO(IServiceProvider services) : IDiscordIO
{
    private DiscordClient Client => services.GetRequiredService<DiscordClient>();

    public Schedule ReadRetry => DiscordRetry.Reads;

    public IO<ChannelSnapshot> GetChannel(ChannelId channel) =>
        this.Channel(channel).Map(ChannelSnapshot.From);

    public IO<UserSnapshot> GetUser(UserId user) =>
        Call(new DiscordOperation("Discord.GetUser", "look up that user", "that user"),
            _ => this.Client.GetUserAsync(user.Value)).Map(UserSnapshot.From);

    public IO<MemberSnapshot> GetMember(GuildId guild, UserId user) =>
        from found in this.Guild(guild)
        from member in Call(new DiscordOperation("Discord.GetMember", "look up that member", "that member", guild),
            _ => found.GetMemberAsync(user.Value))
        select MemberSnapshot.From(member);

    public IO<RoleSnapshot> GetRole(GuildId guild, RoleId role) =>
        from found in this.Guild(guild)
        from value in Call(new DiscordOperation("Discord.GetRole", "look up that role", "that role", guild),
            _ => found.GetRoleAsync(role.Value))
        select RoleSnapshot.From(guild, value);

    public IO<MessageSnapshot> GetMessage(ChannelId channel, MessageId message) =>
        from found in this.Channel(channel)
        from value in Call(Operation(found, "Discord.GetMessage", "look up that message", "that message",
                "Read Message History"),
            _ => found.GetMessageAsync(message.Value))
        select MessageSnapshot.From(value);

    /// <summary>Not retried (Q25). Upload streams are read once, so nothing rewinds them.</summary>
    public IO<MessageId> SendMessage(ChannelId channel, DiscordMessageBuilder message) =>
        from found in this.Channel(channel)
        from sent in Call(Operation(found, "Discord.SendMessage", $"send a message in <#{channel.Value}>",
                $"<#{channel.Value}>", "Send Messages"),
            _ => found.SendMessageAsync(message))
        select new MessageId(sent.Id);

    public IO<Unit> SendDirectMessage(UserId user, DiscordMessageBuilder message) =>
        from found in Call(new DiscordOperation("Discord.GetUser", "look up that user", "that user"),
            _ => this.Client.GetUserAsync(user.Value))
        from _ in Call(new DiscordOperation("Discord.SendDirectMessage", "send them a direct message", "that user"),
            async _ => await found.SendMessageAsync(message))
        select unit;

    /// <summary>A message, or its channel, that's already gone counts as deleted (spec 1.4, section 4).</summary>
    public IO<Unit> DeleteMessage(ChannelId channel, MessageId message, string reason) =>
        Attempt(ChannelOperation(channel), _ => this.Client.GetChannelAsync(channel.Value)).Bind(found => found.Match(
            Fail: AlreadyGoneIsDone,
            Succ: value => Attempt(
                    Operation(value, "Discord.DeleteMessage", "delete that message", "that message",
                        "Manage Messages"),
                    async _ =>
                    {
                        var target = await value.GetMessageAsync(message.Value);
                        await target.DeleteAsync(reason);
                        return unit;
                    })
                .Bind(deleted => deleted.Match(Succ: IO.pure, Fail: AlreadyGoneIsDone))));

    private IO<DiscordGuild> Guild(GuildId guild) =>
        Call(new DiscordOperation("Discord.GetGuild", "reach that server", "that server", guild, IsGuildLookup: true),
            _ => this.Client.GetGuildAsync(guild.Value));

    private IO<DiscordChannel> Channel(ChannelId channel) =>
        Call(ChannelOperation(channel), _ => this.Client.GetChannelAsync(channel.Value));

    private static DiscordOperation ChannelOperation(ChannelId channel) =>
        new("Discord.GetChannel", $"see <#{channel.Value}>", "that channel", Channel: channel);

    /// <summary>An operation on a channel already found, so its guild is known for the refusal.</summary>
    private static DiscordOperation Operation(DiscordChannel channel, string name, string action, string target,
        string permission) =>
        new(name, action, target,
            Optional(channel.GuildId).Map(id => new GuildId(id)),
            channel.GetChannelId(),
            permission);

    private static IO<Unit> AlreadyGoneIsDone(Error error) =>
        error is NotFound ? IO.pure(unit) : IO.fail<Unit>(error);

    private static IO<A> Call<A>(DiscordOperation operation, Func<CancellationToken, Task<A>> call) =>
        Attempt(operation, call).Bind(result => result.Match(Succ: IO.pure, Fail: IO.fail<A>));

    /// <summary>Runs the call and turns any exception into a value, through <see cref="DiscordErrors" />.</summary>
    internal static IO<Fin<A>> Attempt<A>(DiscordOperation operation, Func<CancellationToken, Task<A>> call) =>
        IO.liftAsync(async env =>
        {
            try
            {
                return Fin.Succ(await call(env.Token));
            }
            catch (Exception exception)
            {
                return Fin.Fail<A>(DiscordErrors.Classify(exception, operation, env.Token));
            }
        });
}
