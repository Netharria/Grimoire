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
    private static readonly DiscordPermissions _voiceLock = PermissionValues.VoiceLockPermissions;
    private static readonly DiscordPermissions _other = new(DiscordPermission.ViewChannel, DiscordPermission.AttachFiles);

    private static readonly TheoryData<string, DiscordPermissions, DiscordPermissions> _overwrites = new()
    {
        { "no overwrite", DiscordPermissions.None, DiscordPermissions.None },
        { "explicit send allowed", new DiscordPermissions(DiscordPermission.SendMessages), DiscordPermissions.None },
        { "all lock bits allowed", _lock, DiscordPermissions.None },
        { "send denied", DiscordPermissions.None, new DiscordPermissions(DiscordPermission.SendMessages) },
        { "other bits allowed", _other, DiscordPermissions.None },
        { "other bits denied (private channel)", DiscordPermissions.None, _other },
        { "everything mixed", _lock | _other, DiscordPermissions.None },
        { "allow and deny mixed", new DiscordPermissions(DiscordPermission.SendMessages, DiscordPermission.ViewChannel),
            new DiscordPermissions(DiscordPermission.AddReactions, DiscordPermission.AttachFiles) },
        { "voice bits allowed", new DiscordPermissions(DiscordPermission.Connect, DiscordPermission.Speak),
            DiscordPermissions.None },
        { "voice bits mixed", new DiscordPermissions(DiscordPermission.Connect),
            new DiscordPermissions(DiscordPermission.Speak, DiscordPermission.AttachFiles) }
    };

    /// <summary>Every overwrite, locked with the text set and with the voice and stage set.</summary>
    public static TheoryData<string, DiscordPermissions, DiscordPermissions, DiscordPermissions> Overwrites()
    {
        var data = new TheoryData<string, DiscordPermissions, DiscordPermissions, DiscordPermissions>();
        foreach (var (setName, lockSet) in new[] { ("text", _lock), ("voice", _voiceLock), ("category", _lock | _voiceLock) })
            foreach (var row in _overwrites)
            {
                var (name, allowed, denied) = row.Data;
                data.Add($"{setName}: {name}", allowed, denied, lockSet);
            }

        return data;
    }

    private static (DiscordPermissions Allowed, DiscordPermissions Denied) Lock(
        DiscordPermissions allowed, DiscordPermissions denied, DiscordPermissions lockSet)
        => (allowed.RevokeLockPermissions(lockSet), denied.SetLockPermissions(lockSet));

    [Theory]
    [MemberData(nameof(Overwrites))]
    public void Unlocking_restores_the_overwrite_when_only_the_lock_bits_were_stored(
        string _, DiscordPermissions allowed, DiscordPermissions denied, DiscordPermissions lockSet)
    {
        var (lockedAllowed, lockedDenied) = Lock(allowed, denied, lockSet);

        lockedAllowed.RevertLockPermissions(allowed.GetLockPermissions(lockSet), lockSet).ShouldBe(allowed);
        lockedDenied.RevertLockPermissions(denied.GetLockPermissions(lockSet), lockSet).ShouldBe(denied);
    }

    [Theory]
    [MemberData(nameof(Overwrites))]
    public void Unlocking_restores_the_overwrite_when_every_bit_was_stored(
        string _, DiscordPermissions allowed, DiscordPermissions denied, DiscordPermissions lockSet)
    {
        var (lockedAllowed, lockedDenied) = Lock(allowed, denied, lockSet);

        lockedAllowed.RevertLockPermissions(allowed, lockSet).ShouldBe(allowed);
        lockedDenied.RevertLockPermissions(denied, lockSet).ShouldBe(denied);
    }

    [Fact]
    public void Permissions_changed_while_locked_are_kept_and_only_the_lock_bits_go_back()
    {
        var previousAllowed = new DiscordPermissions(DiscordPermission.SendMessages);
        var (lockedAllowed, lockedDenied) = Lock(previousAllowed, DiscordPermissions.None, _lock);

        // A moderator grants attach files while the channel is locked.
        var changedAllowed = lockedAllowed | DiscordPermission.AttachFiles;

        changedAllowed.RevertLockPermissions(previousAllowed.GetLockPermissions(_lock), _lock)
            .ShouldBe(new DiscordPermissions(DiscordPermission.SendMessages, DiscordPermission.AttachFiles));
        lockedDenied.RevertLockPermissions(DiscordPermissions.None, _lock).ShouldBe(DiscordPermissions.None);
    }

    [Fact]
    public void The_long_overload_restores_the_same_result()
    {
        var allowed = new DiscordPermissions(DiscordPermission.SendMessages);
        var (lockedAllowed, _) = Lock(allowed, DiscordPermissions.None, _lock);

        lockedAllowed.RevertLockPermissions(long.Parse(allowed.GetLockPermissions(_lock).ToString()), _lock)
            .ShouldBe(allowed);
    }

    [Theory]
    [InlineData(DiscordChannelType.Text, true, false)]
    [InlineData(DiscordChannelType.GuildForum, true, false)]
    [InlineData(DiscordChannelType.Voice, false, true)]
    [InlineData(DiscordChannelType.Stage, false, true)]
    [InlineData(DiscordChannelType.Category, true, true)]
    public void Each_channel_type_locks_with_its_set(DiscordChannelType channelType, bool text, bool voice)
        => PermissionValues.LockPermissionsFor(channelType)
            .ShouldBe((text ? _lock : DiscordPermissions.None) | (voice ? _voiceLock : DiscordPermissions.None));
}
