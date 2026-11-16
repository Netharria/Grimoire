// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Entities;
using Grimoire.Functional.Capabilities;
using Grimoire.Functional.Discord;
using LanguageExt;
using Unit = LanguageExt.Unit;

namespace Grimoire.UnitTests.Functional.Fakes;

/// <summary>
///     Discord as a small in-memory guild (spec 1.4, section 3.12). A lookup of something it doesn't hold is
///     <c>NotFound</c>, as Discord would answer, so "deleted while the handler ran" needs no setup. Every call is
///     recorded, in order, in the shared <see cref="Journal" /> as <c>discord Name</c>, and any operation can be told to
///     fail by its name ("Discord.GetMember"). Its retry has no delay.
/// </summary>
public sealed class FakeDiscord(Journal journal) : IDiscordIO
{
    private readonly Atom<HashMap<ChannelId, ChannelSnapshot>> _channels = Atom(HashMap<ChannelId, ChannelSnapshot>());
    private readonly Atom<Seq<(UserId User, DiscordMessageBuilder Message)>> _directMessages =
        Atom(Seq<(UserId, DiscordMessageBuilder)>());

    private readonly Atom<HashMap<string, (Error Error, int Remaining)>> _failures =
        Atom(HashMap<string, (Error, int)>());

    private readonly Atom<HashMap<(GuildId, UserId), MemberSnapshot>> _members =
        Atom(HashMap<(GuildId, UserId), MemberSnapshot>());

    private readonly Atom<HashMap<(ChannelId, MessageId), MessageSnapshot>> _messages =
        Atom(HashMap<(ChannelId, MessageId), MessageSnapshot>());

    private readonly Atom<HashMap<(GuildId, RoleId), RoleSnapshot>> _roles =
        Atom(HashMap<(GuildId, RoleId), RoleSnapshot>());

    private readonly Atom<Seq<(ChannelId Channel, DiscordMessageBuilder Message)>> _sent =
        Atom(Seq<(ChannelId, DiscordMessageBuilder)>());

    private readonly Atom<HashMap<UserId, UserSnapshot>> _users = Atom(HashMap<UserId, UserSnapshot>());
    private readonly Atom<Seq<(ChannelId Channel, MessageId Message)>> _deleted = Atom(Seq<(ChannelId, MessageId)>());
    private readonly Atom<ulong> _nextMessageId = Atom(9_000UL);

    public Seq<(ChannelId Channel, DiscordMessageBuilder Message)> Sent => this._sent.Value;
    public Seq<(UserId User, DiscordMessageBuilder Message)> DirectMessages => this._directMessages.Value;
    public Seq<(ChannelId Channel, MessageId Message)> Deleted => this._deleted.Value;

    public Schedule ReadRetry => Schedule.recurs(3);

    public IO<ChannelSnapshot> GetChannel(ChannelId channel) =>
        this.Call("Discord.GetChannel", () => this._channels.Value.Find(channel), "That channel");

    public IO<UserSnapshot> GetUser(UserId user) =>
        this.Call("Discord.GetUser", () => this._users.Value.Find(user), "That user");

    public IO<MemberSnapshot> GetMember(GuildId guild, UserId user) =>
        this.Call("Discord.GetMember", () => this._members.Value.Find((guild, user)), "That member");

    public IO<RoleSnapshot> GetRole(GuildId guild, RoleId role) =>
        this.Call("Discord.GetRole", () => this._roles.Value.Find((guild, role)), "That role");

    public IO<MessageSnapshot> GetMessage(ChannelId channel, MessageId message) =>
        this.Call("Discord.GetMessage", () => this._messages.Value.Find((channel, message)), "That message");

    public IO<MessageId> SendMessage(ChannelId channel, DiscordMessageBuilder message) =>
        this.Call("Discord.SendMessage", () =>
        {
            this._sent.Swap(sent => sent.Add((channel, message)));
            return Some(new MessageId(this._nextMessageId.Swap(id => id + 1)));
        }, "That channel");

    public IO<Unit> SendDirectMessage(UserId user, DiscordMessageBuilder message) =>
        this.Call("Discord.SendDirectMessage", () =>
        {
            this._directMessages.Swap(sent => sent.Add((user, message)));
            return Some(unit);
        }, "That user");

    /// <summary>Like the live one: a message that's already gone counts as deleted.</summary>
    public IO<Unit> DeleteMessage(ChannelId channel, MessageId message, string reason) =>
        this.Call("Discord.DeleteMessage", () =>
        {
            this._messages.Swap(messages => messages.Remove((channel, message)));
            this._deleted.Swap(deleted => deleted.Add((channel, message)));
            return Some(unit);
        }, "That message");

    public FakeDiscord Has(ChannelSnapshot channel)
    {
        this._channels.Swap(channels => channels.AddOrUpdate(channel.Id, channel));
        return this;
    }

    public FakeDiscord Has(UserSnapshot user)
    {
        this._users.Swap(users => users.AddOrUpdate(user.Id, user));
        return this;
    }

    public FakeDiscord Has(MemberSnapshot member)
    {
        this._members.Swap(members => members.AddOrUpdate((member.Guild, member.User.Id), member));
        return this.Has(member.User);
    }

    public FakeDiscord Has(RoleSnapshot role)
    {
        this._roles.Swap(roles => roles.AddOrUpdate((role.Guild, role.Id), role));
        return this;
    }

    public FakeDiscord Has(MessageSnapshot message)
    {
        this._messages.Swap(messages => messages.AddOrUpdate((message.Channel, message.Id), message));
        return this;
    }

    /// <summary>The next <paramref name="times" /> calls of the operation fail with <paramref name="error" />.</summary>
    public FakeDiscord Fails(string operation, Error error, int times = int.MaxValue)
    {
        this._failures.Swap(failures => failures.AddOrUpdate(operation, (error, times)));
        return this;
    }

    /// <summary>A 5xx or a timeout, as the live edge reports it: transient, so a read is retried.</summary>
    public static Unavailable Outage(string operation = "Discord.Fake") =>
        Unavailable.From("Discord could not be reached.",
            new DiscordCallException($"Discord call {operation} failed: 503", true,
                new HttpRequestException("503 Service Unavailable")));

    /// <summary>A 403 with code 50013, as the live edge reports it, with its refusal.</summary>
    public static Forbidden MissingPermission(GuildId guild, string permission, Option<ChannelId> channel = default,
        string action = "do that") =>
        new($"I don't have permission to {action}. I need **{permission}**.",
            new BotRefusal(guild, channel, permission, action));

    private IO<A> Call<A>(string operation, Func<Option<A>> answer, string target) =>
        IO.lift<Fin<A>>(() =>
        {
            journal.Add($"discord {operation}");
            if (this.TakeFailure(operation).Case is Error error)
                return Fin.Fail<A>(error);

            return answer().Match(Fin.Succ, () => Fin.Fail<A>(new NotFound($"{target} no longer exists.")));
        }).Bind(result => result.Match(Succ: IO.pure, Fail: IO.fail<A>));

    private Option<Error> TakeFailure(string operation)
    {
        if (this._failures.Value.Find(operation).Case is not (Error error, int remaining and > 0))
            return None;

        this._failures.Swap(failures => failures.SetItem(operation, (error, remaining - 1)));
        return error;
    }
}
