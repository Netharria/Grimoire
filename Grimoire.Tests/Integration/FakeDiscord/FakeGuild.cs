// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text.Json.Nodes;

namespace Grimoire.Tests.Integration.FakeDiscord;

/// <summary>A user as Discord would describe it in gateway and REST payloads.</summary>
public sealed record FakeUser(ulong Id, string Username, bool IsBot = false)
{
    public string Mention => $"<@{this.Id}>";

    /// <summary>How DSharpPlus renders a <c>DiscordMember</c>'s mention, as opposed to a plain user's.</summary>
    public string MemberMention => $"<@!{this.Id}>";

    public JsonObject ToJson() => new()
    {
        ["id"] = Snowflake(this.Id),
        ["username"] = this.Username,
        ["global_name"] = this.Username,
        ["discriminator"] = "0",
        ["avatar"] = null,
        ["bot"] = this.IsBot,
        ["public_flags"] = 0
    };

    internal static string Snowflake(ulong id) => id.ToString(CultureInfo.InvariantCulture);
}

/// <summary>A guild member: the user plus the guild roles it holds.</summary>
public sealed record FakeMember(FakeUser User, IReadOnlyList<ulong> RoleIds);

/// <summary>A guild channel or thread. <paramref name="Type" /> uses Discord's channel type numbers.</summary>
public sealed record FakeChannel(ulong Id, string Name, int Type = FakeChannel.TextType, ulong? ParentId = null)
{
    public const int TextType = 0;
    public const int VoiceType = 2;
    public const int CategoryType = 4;
    public const int AnnouncementType = 5;
    public const int StageType = 13;
    public const int PublicThreadType = 11;
    public const int PrivateThreadType = 12;
    public const int ForumType = 15;

    public string Mention => $"<#{this.Id}>";
    public bool IsThread => this.Type is PublicThreadType or PrivateThreadType;
}

/// <summary>
///     The test guild every integration test starts from. It mirrors the accounts in
///     <c>docs/testing/00-environment.md</c>: an owner, a moderator with ManageMessages, and a plain user.
///     All ids are fixed so recorded payloads and assertions stay stable between runs.
/// </summary>
/// <summary>An invite to the guild. Uses is mutable: a member joining with it bumps the count, as on Discord.</summary>
public sealed class FakeInvite(string code, FakeUser inviter, int uses = 0, int maxUses = 0)
{
    public string Code { get; } = code;
    public FakeUser Inviter { get; } = inviter;
    public int Uses { get; set; } = uses;
    public int MaxUses { get; } = maxUses;
}

/// <param name="idOffset">
///     Added to the guild's own ids (guild, channels and roles) so a second guild can share the same users. The
///     default guild uses 0.
/// </param>
public sealed class FakeGuild(ulong idOffset = 0)
{
    /// <summary>The guild's invites, as <c>GET guilds/{id}/invites</c> returns them.</summary>
    public List<FakeInvite> Invites { get; } = [];

    public JsonObject InviteJson(FakeInvite invite) => new()
    {
        ["code"] = invite.Code,
        ["type"] = 0,
        ["guild"] = new JsonObject { ["id"] = FakeUser.Snowflake(this.Id), ["name"] = "Test Guild" },
        ["channel"] = new JsonObject { ["id"] = FakeUser.Snowflake(this.General.Id), ["name"] = this.General.Name, ["type"] = 0 },
        ["inviter"] = invite.Inviter.ToJson(),
        ["uses"] = invite.Uses,
        ["max_uses"] = invite.MaxUses,
        ["max_age"] = 0,
        ["temporary"] = false,
        ["created_at"] = DateTimeOffset.UtcNow.AddDays(-1).ToString("O")
    };

    public const ulong AdministratorPermission = 1UL << 3;
    public const ulong ManageMessagesPermission = 1UL << 13;
    public const ulong ManageGuildPermission = 1UL << 5;
    public const ulong KickMembersPermission = 1UL << 1;
    public const ulong BanMembersPermission = 1UL << 2;
    public const ulong ManageChannelsPermission = 1UL << 4;
    public const ulong ViewAuditLogPermission = 1UL << 7;
    public const ulong ManageRolesPermission = 1UL << 28;
    public const ulong ViewChannelPermission = 1UL << 10;
    public const ulong SendMessagesPermission = 1UL << 11;
    public const ulong EmbedLinksPermission = 1UL << 14;

    private readonly Dictionary<ulong, IReadOnlyList<ulong>> _roleChanges = [];
    private readonly HashSet<ulong> _departed = [];

    public ulong Id { get; } = 1_100_000_000_000_000_001 + idOffset;
    public string Name { get; } = "Grimoire Test Guild";

    /// <summary>Discord gives a bot's application the same id as its user.</summary>
    public ulong ApplicationId => this.Bot.Id;

    public FakeUser Bot { get; } = new(1_100_000_000_000_000_010, "Grimoire", true);
    public FakeUser Owner { get; } = new(1_100_000_000_000_000_020, "owner");
    public FakeUser Moderator { get; } = new(1_100_000_000_000_000_021, "mod");
    public FakeUser User1 { get; } = new(1_100_000_000_000_000_022, "user1");
    public FakeUser Admin { get; } = new(1_100_000_000_000_000_023, "admin");

    /// <summary>A member whose top role sits above the bot's, so the bot can't act on them.</summary>
    public FakeUser Higher { get; } = new(1_100_000_000_000_000_024, "higher");

    public ulong BotRoleId { get; } = 1_100_000_000_000_000_101 + idOffset;
    public ulong ModeratorRoleId { get; } = 1_100_000_000_000_000_102 + idOffset;
    public ulong AdminRoleId { get; } = 1_100_000_000_000_000_103 + idOffset;

    /// <summary>A role holding only ManageGuild, which nobody has until a test gives it with <see cref="SetRoles" />.</summary>
    public ulong ManagerRoleId { get; } = 1_100_000_000_000_000_104 + idOffset;

    /// <summary>A role holding only BanMembers, which nobody has until a test gives it.</summary>
    public ulong BannerRoleId { get; } = 1_100_000_000_000_000_105 + idOffset;

    /// <summary>A role holding only KickMembers, which nobody has until a test gives it.</summary>
    public ulong KickerRoleId { get; } = 1_100_000_000_000_000_107 + idOffset;

    /// <summary>The top role, above the bot's.</summary>
    public ulong HigherRoleId { get; } = 1_100_000_000_000_000_106 + idOffset;

    private const int BotRolePosition = 2;
    private const int HigherRolePosition = 3;

    public FakeChannel General { get; } = new(1_100_000_000_000_000_201 + idOffset, "general");
    public FakeChannel ModLog { get; } = new(1_100_000_000_000_000_202 + idOffset, "modlog");
    public FakeChannel Forum { get; } = new(1_100_000_000_000_000_203 + idOffset, "forum", FakeChannel.ForumType);

    /// <summary>
    ///     A channel whose overwrite denies the bot's role View Channel and Send Messages. Administrator ignores
    ///     overwrites, so this only bites when a test starts the bot with narrower <see cref="BotPermissions" />.
    /// </summary>
    public FakeChannel Private { get; } = new(1_100_000_000_000_000_204 + idOffset, "private");

    public FakeChannel Category { get; } = new(1_100_000_000_000_000_205 + idOffset, "Text Channels", FakeChannel.CategoryType);
    public FakeChannel Voice { get; } = new(1_100_000_000_000_000_206 + idOffset, "voice", FakeChannel.VoiceType);
    public FakeChannel Stage { get; } = new(1_100_000_000_000_000_207 + idOffset, "stage", FakeChannel.StageType);
    public FakeChannel Announcements { get; } = new(1_100_000_000_000_000_208 + idOffset, "news", FakeChannel.AnnouncementType);

    // #private starts with an overwrite denying the bot's role View Channel and Send Messages.
    private readonly Dictionary<ulong, Dictionary<ulong, (int Type, ulong Allow, ulong Deny)>> _overwrites = new()
    {
        [1_100_000_000_000_000_204 + idOffset] = new()
        {
            [1_100_000_000_000_000_101 + idOffset] = (0, 0, ViewChannelPermission | SendMessagesPermission)
        }
    };
    private readonly Dictionary<ulong, (string Name, ulong Permissions, int Position)> _addedRoles = [];
    private ulong _nextRoleId = 1_100_000_000_000_000_150 + idOffset;

    /// <summary>
    ///     The bot role's guild permissions. The default is Administrator. Set this before
    ///     <c>GrimoireTestBot.StartAsync</c> sends <c>GUILD_CREATE</c>, to test what happens when the bot lacks a
    ///     permission.
    /// </summary>
    public ulong BotPermissions { get; set; } = AdministratorPermission;

    public FakeChannel PublicThread { get; } =
        new(1_100_000_000_000_000_211 + idOffset, "public-thread", FakeChannel.PublicThreadType, 1_100_000_000_000_000_201 + idOffset);

    public FakeChannel PrivateThread { get; } =
        new(1_100_000_000_000_000_212 + idOffset, "private-thread", FakeChannel.PrivateThreadType, 1_100_000_000_000_000_201 + idOffset);

    public FakeChannel ForumPost { get; } =
        new(1_100_000_000_000_000_213 + idOffset, "forum-post", FakeChannel.PublicThreadType, 1_100_000_000_000_000_203 + idOffset);

    private readonly HashSet<ulong> _deletedChannels = [];

    public IReadOnlyList<FakeChannel> Channels
        => [.. new[] { this.General, this.ModLog, this.Forum, this.Private, this.Category, this.Voice, this.Stage, this.Announcements }
            .Where(channel => !this._deletedChannels.Contains(channel.Id))];

    /// <summary>Removes a channel or thread. Use <c>GrimoireTestBot.DeleteChannel</c> so the bot hears about it.</summary>
    public void DeleteChannel(FakeChannel channel) => this._deletedChannels.Add(channel.Id);


    /// <summary>Sets a channel permission overwrite for a role (type 0) or member (type 1).</summary>
    public void SetOverwrite(ulong channelId, ulong targetId, int type, ulong allow, ulong deny)
    {
        lock (this._overwrites)
        {
            if (!this._overwrites.TryGetValue(channelId, out var channelOverwrites))
                this._overwrites[channelId] = channelOverwrites = [];
            channelOverwrites[targetId] = (type, allow, deny);
        }
    }

    public (ulong Allow, ulong Deny)? OverwriteOf(ulong channelId, ulong targetId)
    {
        lock (this._overwrites)
            return this._overwrites.TryGetValue(channelId, out var channelOverwrites)
                   && channelOverwrites.TryGetValue(targetId, out var overwrite)
                ? (overwrite.Allow, overwrite.Deny)
                : null;
    }

    /// <summary>Whether the bot can see a channel: Administrator, or no overwrite on its role denying View Channel.</summary>
    public bool BotCanView(ulong channelId)
        => this.BotHas(AdministratorPermission)
           || ((this.OverwriteOf(channelId, this.BotRoleId)?.Deny ?? 0) & ViewChannelPermission) == 0;

    /// <summary>Whether the bot can post in a guild channel: Administrator, or no overwrite on its role denying View or Send.</summary>
    public bool BotCanSend(ulong channelId)
        => this.BotHas(AdministratorPermission)
           || ((this.OverwriteOf(channelId, this.BotRoleId)?.Deny ?? 0) & (ViewChannelPermission | SendMessagesPermission)) == 0;

    /// <summary>
    ///     Adds a role, as if a moderator created it, at <paramref name="position" />. The default sits below the
    ///     bot's role; use <see cref="RoleAboveTheBot" /> for one the bot can't manage. Returns the new role's id.
    /// </summary>
    public ulong AddRole(string name, ulong permissions = 0, int position = 1)
    {
        lock (this._addedRoles)
        {
            var id = this._nextRoleId++;
            this._addedRoles[id] = (name, permissions, position);
            return id;
        }
    }

    public const int RoleAboveTheBot = BotRolePosition + 1;

    /// <summary>Whether the bot can grant or revoke a role: it needs Manage Roles and a higher top role.</summary>
    public bool BotCanManageRole(ulong roleId)
        => this.BotHas(ManageRolesPermission) && this.PositionOf(roleId) < this.HierarchyOf(this.Bot.Id);

    /// <summary>Gives a member a role, as Discord does when the bot grants it.</summary>
    public void GrantRole(FakeUser user, ulong roleId)
        => this.SetRoles(user, [.. this.GetMember(user).RoleIds.Append(roleId).Distinct()]);

    public void RevokeRole(FakeUser user, ulong roleId)
        => this.SetRoles(user, [.. this.GetMember(user).RoleIds.Where(id => id != roleId)]);

    public bool RemoveRole(ulong roleId)
    {
        lock (this._addedRoles)
            return this._addedRoles.Remove(roleId);
    }

    public bool HasRole(ulong roleId)
    {
        lock (this._addedRoles)
            return this._addedRoles.ContainsKey(roleId) || this.FixedRoles().Any(role => role.Id == roleId);
    }

    public JsonObject? RoleJsonOf(ulong roleId)
    {
        lock (this._addedRoles)
            return this.FixedRoles().Concat(this._addedRoles.Select(role => (Id: role.Key, role.Value.Name, role.Value.Position, role.Value.Permissions)))
                .Where(role => role.Id == roleId)
                .Select(role => RoleJson(role.Id, role.Name, role.Position, role.Permissions))
                .FirstOrDefault();
    }

    private IEnumerable<(ulong Id, string Name, int Position, ulong Permissions)> FixedRoles() =>
    [
        (this.Id, "@everyone", 0, 0),
        (this.BotRoleId, "Grimoire", BotRolePosition, this.BotPermissions),
        (this.HigherRoleId, "Higher", HigherRolePosition, 0),
        (this.BannerRoleId, "Banner", 1, BanMembersPermission),
        (this.KickerRoleId, "Kicker", 1, KickMembersPermission),
        (this.ModeratorRoleId, "Moderator", 1, ManageMessagesPermission),
        (this.AdminRoleId, "Admin", 1, AdministratorPermission),
        (this.ManagerRoleId, "Manager", 1, ManageGuildPermission)
    ];

    private IEnumerable<JsonNode> AllRoleJson()
    {
        lock (this._addedRoles)
            return [.. this.FixedRoles().Concat(this._addedRoles.Select(role => (Id: role.Key, role.Value.Name, role.Value.Position, role.Value.Permissions)))
                .Select(role => (JsonNode)RoleJson(role.Id, role.Name, role.Position, role.Permissions))];
    }

    /// <summary>Active threads, sent in <c>GUILD_CREATE</c>'s <c>threads</c> array as Discord does.</summary>
    public IReadOnlyList<FakeChannel> Threads
        => [.. new[] { this.PublicThread, this.PrivateThread, this.ForumPost }.Where(thread => !this._deletedChannels.Contains(thread.Id))];

    public IReadOnlyList<FakeMember> Members =>
    [
        .. new FakeMember[]
        {
            new(this.Bot, [this.BotRoleId]),
            new(this.Owner, []),
            new(this.Moderator, [this.ModeratorRoleId]),
            new(this.User1, []),
            new(this.Admin, [this.AdminRoleId]),
            new(this.Higher, [this.HigherRoleId])
        }
        .Where(member => !this._departed.Contains(member.User.Id))
        .Select(member => this._roleChanges.TryGetValue(member.User.Id, out var roles) ? member with { RoleIds = roles } : member)
    ];

    /// <summary>The member leaves the guild. Use <c>GrimoireTestBot.MemberLeaves</c> so the bot hears about it.</summary>
    public void Depart(FakeUser user) => this._departed.Add(user.Id);

    /// <summary>The member joins again, with no roles, as on Discord.</summary>
    public void Return(FakeUser user)
    {
        this._departed.Remove(user.Id);
        this._roleChanges[user.Id] = [];
    }

    /// <summary>
    ///     Changes a member's roles from now on. Interactions carry the member's current roles, and DSharpPlus updates
    ///     its cache from them, so the next command the member runs sees the change.
    /// </summary>
    public void SetRoles(FakeUser user, params ulong[] roleIds) => this._roleChanges[user.Id] = roleIds;

    public FakeMember GetMember(FakeUser user) => this.Members.Single(member => member.User.Id == user.Id);

    /// <summary>Effective guild permissions as Discord reports them on an interaction's <c>member.permissions</c>.</summary>
    public ulong GuildPermissionsOf(FakeUser user) => user.Id == this.Owner.Id
        ? AdministratorPermission
        : this.GetMember(user).RoleIds.Aggregate(0UL, (permissions, roleId) => permissions | this.RolePermissions(roleId));

    private ulong RolePermissions(ulong roleId)
        => roleId == this.BotRoleId ? this.BotPermissions
            : roleId == this.AdminRoleId ? AdministratorPermission
            : roleId == this.ModeratorRoleId ? ManageMessagesPermission
            : roleId == this.ManagerRoleId ? ManageGuildPermission
            : roleId == this.BannerRoleId ? BanMembersPermission
            : roleId == this.KickerRoleId ? KickMembersPermission
            : this.AddedRolePermissions(roleId);

    private ulong AddedRolePermissions(ulong roleId)
    {
        lock (this._addedRoles)
            return this._addedRoles.TryGetValue(roleId, out var role) ? role.Permissions : 0;
    }

    /// <summary>
    ///     Discord's ranking: the owner outranks everyone, otherwise a member ranks at their highest role's
    ///     position. Someone who isn't a member ranks lowest.
    /// </summary>
    public int HierarchyOf(ulong userId)
        => userId == this.Owner.Id ? int.MaxValue
            : this.Members.FirstOrDefault(member => member.User.Id == userId) is { } member
                ? member.RoleIds.Select(this.PositionOf).DefaultIfEmpty(0).Max()
                : -1;

    private int PositionOf(ulong roleId)
    {
        if (roleId == this.HigherRoleId)
            return HigherRolePosition;
        if (roleId == this.BotRoleId)
            return BotRolePosition;
        lock (this._addedRoles)
            return this._addedRoles.TryGetValue(roleId, out var role) ? role.Position : 1;
    }

    /// <summary>Whether the bot holds every one of <paramref name="permissions" />, counting Administrator.</summary>
    public bool BotHas(ulong permissions)
        => (this.BotPermissions & AdministratorPermission) != 0 || (this.BotPermissions & permissions) == permissions;

    public JsonObject ReadyPayload() => new()
    {
        ["v"] = 10,
        ["user"] = this.Bot.ToJson(),
        ["guilds"] = new JsonArray(new JsonObject { ["id"] = FakeUser.Snowflake(this.Id), ["unavailable"] = true }),
        ["private_channels"] = new JsonArray(),
        ["session_id"] = "fake-session",
        ["resume_gateway_url"] = "wss://gateway.fake.discord.invalid",
        ["shard"] = new JsonArray(0, 1),
        ["application"] = new JsonObject { ["id"] = FakeUser.Snowflake(this.ApplicationId), ["flags"] = 0 }
    };

    public JsonObject GuildCreatePayload() => new()
    {
        ["id"] = FakeUser.Snowflake(this.Id),
        ["name"] = this.Name,
        ["icon"] = null,
        ["splash"] = null,
        ["owner_id"] = FakeUser.Snowflake(this.Owner.Id),
        ["afk_timeout"] = 300,
        ["verification_level"] = 0,
        ["default_message_notifications"] = 0,
        ["explicit_content_filter"] = 0,
        ["mfa_level"] = 0,
        ["premium_tier"] = 0,
        ["nsfw_level"] = 0,
        ["system_channel_flags"] = 0,
        ["preferred_locale"] = "en-US",
        ["features"] = new JsonArray(),
        ["joined_at"] = "2024-01-01T00:00:00.000000+00:00",
        ["large"] = false,
        ["unavailable"] = false,
        ["member_count"] = this.Members.Count,
        ["roles"] = new JsonArray([.. this.AllRoleJson()]),
        ["channels"] = new JsonArray([.. this.Channels.Select((channel, index) => (JsonNode)this.ChannelJson(channel, index))]),
        ["members"] = new JsonArray([.. this.Members.Select(member => (JsonNode)MemberJson(member))]),
        ["threads"] = new JsonArray([.. this.Threads.Select(thread => (JsonNode)this.ChannelJson(thread))]),
        ["presences"] = new JsonArray(),
        ["voice_states"] = new JsonArray(),
        ["emojis"] = new JsonArray(),
        ["stickers"] = new JsonArray(),
        ["stage_instances"] = new JsonArray(),
        ["guild_scheduled_events"] = new JsonArray()
    };

    public JsonObject ChannelJson(FakeChannel channel, int position = 0)
    {
        var json = new JsonObject
        {
            ["id"] = FakeUser.Snowflake(channel.Id),
            ["type"] = channel.Type,
            ["guild_id"] = FakeUser.Snowflake(this.Id),
            ["name"] = channel.Name,
            ["position"] = position,
            ["permission_overwrites"] = this.OverwritesJson(channel.Id),
            ["parent_id"] = channel.ParentId is { } parentId ? FakeUser.Snowflake(parentId) : null,
            ["topic"] = null,
            ["nsfw"] = false,
            ["rate_limit_per_user"] = 0,
            ["last_message_id"] = null
        };
        if (channel.Type is FakeChannel.VoiceType or FakeChannel.StageType)
        {
            json["bitrate"] = 64_000;
            json["user_limit"] = 0;
        }

        if (channel.IsThread)
        {
            json.Remove("permission_overwrites");
            json["owner_id"] = FakeUser.Snowflake(this.Moderator.Id);
            json["member_count"] = 2;
            json["message_count"] = 0;
            json["thread_metadata"] = new JsonObject
            {
                ["archived"] = false,
                ["locked"] = false,
                ["auto_archive_duration"] = 1440,
                ["archive_timestamp"] = "2024-01-01T00:00:00.000000+00:00"
            };
        }

        return json;
    }

    private JsonArray OverwritesJson(ulong channelId)
    {
        lock (this._overwrites)
            return new JsonArray([
                .. (this._overwrites.GetValueOrDefault(channelId) ?? []).Select(overwrite => (JsonNode)new JsonObject
                {
                    ["id"] = FakeUser.Snowflake(overwrite.Key),
                    ["type"] = overwrite.Value.Type,
                    ["allow"] = overwrite.Value.Allow.ToString(CultureInfo.InvariantCulture),
                    ["deny"] = overwrite.Value.Deny.ToString(CultureInfo.InvariantCulture)
                })
            ]);
    }

    public static JsonObject MemberJson(FakeMember member, bool includeUser = true)
    {
        var json = new JsonObject
        {
            ["nick"] = null,
            ["avatar"] = null,
            ["roles"] = new JsonArray([.. member.RoleIds.Select(id => (JsonNode)FakeUser.Snowflake(id))]),
            ["joined_at"] = "2024-01-01T00:00:00.000000+00:00",
            ["deaf"] = false,
            ["mute"] = false,
            ["flags"] = 0,
            ["pending"] = false,
            ["communication_disabled_until"] = null
        };
        if (includeUser)
            json["user"] = member.User.ToJson();
        return json;
    }

    private static JsonObject RoleJson(ulong id, string name, int position, ulong permissions) => new()
    {
        ["id"] = FakeUser.Snowflake(id),
        ["name"] = name,
        ["color"] = 0,
        ["hoist"] = false,
        ["icon"] = null,
        ["unicode_emoji"] = null,
        ["position"] = position,
        ["permissions"] = permissions.ToString(CultureInfo.InvariantCulture),
        ["managed"] = false,
        ["mentionable"] = false,
        ["flags"] = 0
    };
}
