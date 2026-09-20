// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Entities;
using Grimoire.Extensions;
using Grimoire.Structs;

namespace Grimoire.Tests.Extensions;

/// <summary>
///     Locking rewrites the @everyone overwrite and unlocking has to put it back exactly, whatever the channel had
///     before. These tests run the same steps as <c>Lock</c> and <c>Unlock</c>, for both the value stored by the
///     current code (only the lock bits) and by older versions (every bit).
/// </summary>
public sealed class LockPermissionsRoundTripTests
{
    private static readonly DiscordPermissions _lock = PermissionValues.LockPermissions;
    private static readonly DiscordPermissions _other = new(DiscordPermission.ViewChannel, DiscordPermission.AttachFiles);

    public static TheoryData<string, DiscordPermissions, DiscordPermissions> Overwrites() => new()
    {
        { "no overwrite", DiscordPermissions.None, DiscordPermissions.None },
        { "explicit send allowed", new DiscordPermissions(DiscordPermission.SendMessages), DiscordPermissions.None },
        { "all lock bits allowed", _lock, DiscordPermissions.None },
        { "send denied", DiscordPermissions.None, new DiscordPermissions(DiscordPermission.SendMessages) },
        { "other bits allowed", _other, DiscordPermissions.None },
        { "other bits denied (private channel)", DiscordPermissions.None, _other },
        { "everything mixed", _lock | _other, DiscordPermissions.None },
        { "allow and deny mixed", new DiscordPermissions(DiscordPermission.SendMessages, DiscordPermission.ViewChannel),
            new DiscordPermissions(DiscordPermission.AddReactions, DiscordPermission.AttachFiles) }
    };

    private static (DiscordPermissions Allowed, DiscordPermissions Denied) Lock(
        DiscordPermissions allowed, DiscordPermissions denied)
        => (allowed.RevokeLockPermissions(), denied.SetLockPermissions());

    [Theory]
    [MemberData(nameof(Overwrites))]
    public void Unlocking_restores_the_overwrite_when_only_the_lock_bits_were_stored(
        string _, DiscordPermissions allowed, DiscordPermissions denied)
    {
        var (lockedAllowed, lockedDenied) = Lock(allowed, denied);

        lockedAllowed.RevertLockPermissions(allowed.GetLockPermissions()).ShouldBe(allowed);
        lockedDenied.RevertLockPermissions(denied.GetLockPermissions()).ShouldBe(denied);
    }

    [Theory]
    [MemberData(nameof(Overwrites))]
    public void Unlocking_restores_the_overwrite_when_every_bit_was_stored(
        string _, DiscordPermissions allowed, DiscordPermissions denied)
    {
        var (lockedAllowed, lockedDenied) = Lock(allowed, denied);

        lockedAllowed.RevertLockPermissions(allowed).ShouldBe(allowed);
        lockedDenied.RevertLockPermissions(denied).ShouldBe(denied);
    }

    [Fact]
    public void Permissions_changed_while_locked_are_kept_and_only_the_lock_bits_go_back()
    {
        var previousAllowed = new DiscordPermissions(DiscordPermission.SendMessages);
        var (lockedAllowed, lockedDenied) = Lock(previousAllowed, DiscordPermissions.None);

        // A moderator grants attach files while the channel is locked.
        var changedAllowed = lockedAllowed | DiscordPermission.AttachFiles;

        changedAllowed.RevertLockPermissions(previousAllowed.GetLockPermissions())
            .ShouldBe(new DiscordPermissions(DiscordPermission.SendMessages, DiscordPermission.AttachFiles));
        lockedDenied.RevertLockPermissions(DiscordPermissions.None).ShouldBe(DiscordPermissions.None);
    }

    [Fact]
    public void The_long_overload_restores_the_same_result()
    {
        var allowed = new DiscordPermissions(DiscordPermission.SendMessages);
        var (lockedAllowed, _) = Lock(allowed, DiscordPermissions.None);

        lockedAllowed.RevertLockPermissions(long.Parse(allowed.GetLockPermissions().ToString())).ShouldBe(allowed);
    }
}
