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
public sealed class FakeGuild
{
    public const ulong AdministratorPermission = 1UL << 3;
    public const ulong ManageMessagesPermission = 1UL << 13;

    public ulong Id { get; } = 1_100_000_000_000_000_001;
    public string Name { get; } = "Grimoire Test Guild";

    /// <summary>Discord gives a bot's application the same id as its user.</summary>
    public ulong ApplicationId => this.Bot.Id;

    public FakeUser Bot { get; } = new(1_100_000_000_000_000_010, "Grimoire", true);
    public FakeUser Owner { get; } = new(1_100_000_000_000_000_020, "owner");
    public FakeUser Moderator { get; } = new(1_100_000_000_000_000_021, "mod");
    public FakeUser User1 { get; } = new(1_100_000_000_000_000_022, "user1");
    public FakeUser Admin { get; } = new(1_100_000_000_000_000_023, "admin");

    public ulong BotRoleId { get; } = 1_100_000_000_000_000_101;
    public ulong ModeratorRoleId { get; } = 1_100_000_000_000_000_102;
    public ulong AdminRoleId { get; } = 1_100_000_000_000_000_103;

    public FakeChannel General { get; } = new(1_100_000_000_000_000_201, "general");
    public FakeChannel ModLog { get; } = new(1_100_000_000_000_000_202, "modlog");
    public FakeChannel Forum { get; } = new(1_100_000_000_000_000_203, "forum", FakeChannel.ForumType);

    public FakeChannel PublicThread { get; } =
        new(1_100_000_000_000_000_211, "public-thread", FakeChannel.PublicThreadType, 1_100_000_000_000_000_201);

    public FakeChannel PrivateThread { get; } =
        new(1_100_000_000_000_000_212, "private-thread", FakeChannel.PrivateThreadType, 1_100_000_000_000_000_201);

    public FakeChannel ForumPost { get; } =
        new(1_100_000_000_000_000_213, "forum-post", FakeChannel.PublicThreadType, 1_100_000_000_000_000_203);

    public IReadOnlyList<FakeChannel> Channels => [this.General, this.ModLog, this.Forum];

    /// <summary>Active threads, sent in <c>GUILD_CREATE</c>'s <c>threads</c> array as Discord does.</summary>
    public IReadOnlyList<FakeChannel> Threads => [this.PublicThread, this.PrivateThread, this.ForumPost];

    public IReadOnlyList<FakeMember> Members =>
    [
        new(this.Bot, [this.BotRoleId]),
        new(this.Owner, []),
        new(this.Moderator, [this.ModeratorRoleId]),
        new(this.User1, []),
        new(this.Admin, [this.AdminRoleId])
    ];

    public FakeMember GetMember(FakeUser user) => this.Members.Single(member => member.User.Id == user.Id);

    /// <summary>Effective guild permissions as Discord reports them on an interaction's <c>member.permissions</c>.</summary>
    public ulong GuildPermissionsOf(FakeUser user) => user.Id == this.Owner.Id
        ? AdministratorPermission
        : this.GetMember(user).RoleIds.Aggregate(0UL, (permissions, roleId) => permissions | this.RolePermissions(roleId));

    private ulong RolePermissions(ulong roleId)
        => roleId == this.BotRoleId || roleId == this.AdminRoleId ? AdministratorPermission
            : roleId == this.ModeratorRoleId ? ManageMessagesPermission
            : 0;

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
        ["roles"] = new JsonArray(
            RoleJson(this.Id, "@everyone", 0, 0),
            RoleJson(this.BotRoleId, "Grimoire", 2, AdministratorPermission),
            RoleJson(this.ModeratorRoleId, "Moderator", 1, ManageMessagesPermission),
            RoleJson(this.AdminRoleId, "Admin", 1, AdministratorPermission)),
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
            ["permission_overwrites"] = new JsonArray(),
            ["parent_id"] = channel.ParentId is { } parentId ? FakeUser.Snowflake(parentId) : null,
            ["topic"] = null,
            ["nsfw"] = false,
            ["rate_limit_per_user"] = 0,
            ["last_message_id"] = null
        };
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
