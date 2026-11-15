// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Functional.Capabilities;
using LanguageExt;

namespace Grimoire.Functional.Boundary;

public enum InvocationKind
{
    Slash,
    Text
}

/// <summary>
///     What a command's core may know about who ran it, where and how, copied from the <c>CommandContext</c> by the
///     runner. It holds no Discord type that a test can't construct (plan C5).
/// </summary>
/// <param name="Command">The command's full name, as <c>CommandContext.Command.FullName</c>.</param>
/// <param name="Permissions">The invoker's guild permissions; <c>DiscordPermissions.None</c> outside a guild.</param>
/// <param name="CreatedAt">When Discord created the interaction or message.</param>
public sealed record Invocation(
    string Command,
    InvocationKind Kind,
    Option<GuildId> Guild,
    ChannelId Channel,
    UserId User,
    DiscordPermissions Permissions,
    DateTimeOffset CreatedAt)
{
    /// <summary>The usual test for "moderator" across the bot: Manage Messages in the guild.</summary>
    public bool IsModerator => this.Permissions.HasPermission(DiscordPermission.ManageMessages);
}

/// <summary>Who may see a command's replies. On slash, both private cases are ephemeral (TD-11).</summary>
public abstract record Privacy
{
    private Privacy()
    {
    }

    public static Privacy Public { get; } = new PublicReply();
    public static Privacy Private { get; } = new PrivateReply();
    public static Privacy PrivateByDirectMessage { get; } = new PrivateReplyByDirectMessage();

    public sealed record PublicReply : Privacy;

    /// <summary>Text: a 🔒 reaction on the message, and the command doesn't run.</summary>
    public sealed record PrivateReply : Privacy;

    /// <summary>Text: the command runs, and every reply, refusals included, goes to the invoker's DMs.</summary>
    public sealed record PrivateReplyByDirectMessage : Privacy;
}

public static class DeliveryRules
{
    /// <summary>The reaction a private command gets when it's run as text (plan C11).</summary>
    public const string PrivateOnTextEmoji = "🔒";

    /// <summary>How the replies go out. <c>None</c> means "react 🔒 and stop".</summary>
    [Pure]
    public static Option<Delivery> For(Privacy privacy, InvocationKind kind) =>
        (privacy, kind) switch
        {
            (Privacy.PublicReply, _) => Delivery.Public,
            (_, InvocationKind.Slash) => Delivery.Ephemeral,
            (Privacy.PrivateReply, InvocationKind.Text) => None,
            (Privacy.PrivateReplyByDirectMessage, InvocationKind.Text) => Delivery.DirectMessage,
            // C# 14 can't prove this switch complete. Phase 4's closed Privacy removes the arm.
            _ => throw new UnreachableException($"Unhandled privacy {privacy} for {kind}.")
        };
}

/// <summary>
///     The command's replies are private (TD-11): a 🔒 reaction when run as text, or a DM when
///     <see cref="DirectMessageOnText" /> is set. Without the attribute, replies are public. A command whose privacy
///     depends on data passes a privacy function to the runner instead, and doesn't carry this attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class PrivateReplyAttribute : Attribute
{
    public bool DirectMessageOnText { get; init; }

    public Privacy Privacy => this.DirectMessageOnText ? Privacy.PrivateByDirectMessage : Privacy.Private;
}
