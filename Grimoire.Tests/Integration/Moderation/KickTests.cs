// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text.Json.Nodes;
using Grimoire.Features.Shared.GuildIssues;
using Grimoire.Settings.Enums;
using Grimoire.Tests.Integration.FakeDiscord;

namespace Grimoire.Tests.Integration.Moderation;

/// <summary>
///     <c>/Kick</c>, cases MOD-43 to MOD-56 in <c>docs/testing/02-moderation-actions.md</c>. Kick takes a
///     <c>DiscordMember</c>, which DSharpPlus resolves from the interaction, so KI-19 doesn't apply. The fake refuses
///     kicks of anyone ranked at or above the bot, as Discord does. MOD-56 (rejoining) is Discord's behaviour.
/// </summary>
[Collection("Test collection")]
public sealed class KickTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private const string PermissionError = "You do not have the required permissions to use this command.";

    private readonly GrimoireCoreFactory _database = database;
    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;

    public async ValueTask InitializeAsync()
    {
        await this._database.ResetDatabase();
        this._bot = await StartBotAsync(this._database);
        await this._bot.ConfigureModerationAsync();
    }

    /// <summary>
    ///     <c>KickUser</c> is never registered in production (KI-25), so the test bot adds it to test the command's
    ///     logic. <see cref="Kick_IsRegisteredByTheBot" /> fails until the real registration is fixed.
    /// </summary>
    private static Task<GrimoireTestBot> StartBotAsync(GrimoireCoreFactory database, Action<FakeGuild>? configureGuild = null)
        => GrimoireTestBot.StartAsync(database, configureGuild, [typeof(Grimoire.Features.Moderation.Kick.KickUser)]);

    [Fact(Skip = "Fails: KickUser is never passed to AddCommands in GrimoireServiceRegistration, so /Kick doesn't " +
                 "exist. KI-25")]
    public async Task Kick_IsRegisteredByTheBot()
    {
        await using var plainBot = await GrimoireTestBot.StartAsync(this._database);
        plainBot.Rest.RegisteredCommands.ShouldContainKey("kick");
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    /// <summary>MOD-43: the kick, the sin, the reply, and the same embed in the mod log. No DM is sent.</summary>
    [Fact]
    public async Task Kick_KicksRecordsTheSinRepliesAndLogs()
    {
        var reply = await this.KickAsync(this.Guild.Admin, this.Guild.User1, "rude");

        var embed = reply.Embed!;
        embed["author"]!["name"]!.GetValue<string>().ShouldBe("Kick");
        Field(embed, "User").ShouldBe(this.Guild.User1.MemberMention);
        Field(embed, "Moderator").ShouldBe(this.Guild.Admin.MemberMention);
        Field(embed, "Reason").ShouldBe("rude");

        var remove = this._bot.Rest.Requests.Single(this.IsKickOf(this.Guild.User1));
        remove.AuditLogReason.ShouldBe("rude");
        this._bot.Rest.Kicked.ShouldContain(this.Guild.User1.Id);

        var sin = await this.SingleSinAsync();
        sin.SinType.ShouldBe(SinType.Kick);
        sin.Actor.ShouldBe(new ModerationActor.Moderator(new ModeratorId(this.Guild.Admin.Id)));
        sin.ReasonHistory.ShouldHaveSingleItem().Reason.Value.ShouldBe("rude");
        Field(embed, "Sin Id").ShouldBe($"**{sin.Id.Value.ToString(CultureInfo.InvariantCulture)}**");

        var logged = await this._bot.WaitForModLogEmbedAsync(e => e["author"]?["name"]?.GetValue<string>() == "Kick");
        Field(logged, "Sin Id").ShouldBe(Field(embed, "Sin Id"));
        this._bot.Rest.Requests.Count(request => request.Path == "users/@me/channels").ShouldBe(0);
        this._bot.Rest.Unhandled.ShouldBeEmpty();
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>MOD-44.</summary>
    [Fact]
    public async Task Kick_WithoutAReason_HasNoReasonField_AndStoresNoReason()
    {
        var reply = await this.KickAsync(this.Guild.Admin, this.Guild.User1);

        reply.Embed!["fields"]!.AsArray().Select(field => field!["name"]!.GetValue<string>()).ShouldNotContain("Reason");
        (await this.SingleSinAsync()).ReasonHistory.ShouldBeEmpty();
    }

    /// <summary>MOD-45: Discord's client enforces the reason length.</summary>
    [Fact]
    public void Kick_ReasonIsRegisteredWithAThousandCharacterLimit()
        => this._bot.Rest.RegisteredCommands["kick"]["options"]!.AsArray()
            .Single(option => option!["name"]!.GetValue<string>() == "reason")!["max_length"]!.GetValue<int>().ShouldBe(1000);

    /// <summary>MOD-46 and MOD-47: the gate is Kick Members, so Manage Messages alone is not enough.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Kick_WithoutKickMembers_IsDenied(bool hasManageMessages)
    {
        if (hasManageMessages)
            this.Guild.SetRoles(this.Guild.User1, this.Guild.ModeratorRoleId);

        (await this.KickAsync(this.Guild.User1, this.Guild.Admin)).Description.ShouldBe(PermissionError);

        await this.ShouldHaveNoSinsOrKicksAsync();
    }

    /// <summary>MOD-48 and MOD-49.</summary>
    [Theory]
    [InlineData("kick members only")]
    [InlineData("mod with kick members")]
    public async Task Kick_WithKickMembers_IsAllowed(string who)
    {
        var moderator = who == "kick members only" ? this.Guild.User1 : this.Guild.Moderator;
        this.Guild.SetRoles(moderator, who == "kick members only"
            ? [this.Guild.KickerRoleId]
            : [this.Guild.ModeratorRoleId, this.Guild.KickerRoleId]);
        var target = moderator == this.Guild.User1 ? this.Guild.Moderator : this.Guild.User1;

        (await this.KickAsync(moderator, target)).Embed!["author"]!["name"]!.GetValue<string>().ShouldBe("Kick");

        (await this.SingleSinAsync()).Actor.ShouldBe(new ModerationActor.Moderator(new ModeratorId(moderator.Id)));
    }

    /// <summary>MOD-50.</summary>
    [Fact]
    public async Task Kick_OfYourself_IsRefused()
    {
        (await this.KickAsync(this.Guild.Admin, this.Guild.Admin)).Description.ShouldBe("You can't kick yourself.");

        await this.ShouldHaveNoSinsOrKicksAsync();
    }

    /// <summary>
    ///     MOD-51 and MOD-52: there's no hierarchy pre-check, so Discord refuses and the error handler blames a missing
    ///     permission (misleading, since it's the role order). Because the kick comes first, no sin is left.
    /// </summary>
    [Theory]
    [InlineData("higher")]
    [InlineData("owner")]
    [InlineData("bot")]
    public async Task Kick_OfSomeoneTheBotCantOutrank_ShowsTheMissingPermissionsHelp_AndLeavesNoSin(string who)
    {
        var target = who switch
        {
            "owner" => this.Guild.Owner,
            "bot" => this.Guild.Bot,
            _ => this.Guild.Higher
        };
        var issue = GuildIssueCatalog.BotMissingPermissions(new GuildId(this.Guild.Id), "Kick");

        (await this.KickAsync(this.Guild.Admin, target)).Description
            .ShouldBe($"{issue.Summary}\n**Suggested fix:** {issue.SuggestedFix}");

        await this.ShouldHaveNoSinsOrKicksAsync();
    }

    /// <summary>MOD-53: nothing checks that the moderator outranks the target.</summary>
    [Fact(Skip = "Fails: a moderator can kick someone ranked above them, as long as the bot outranks the target. KI-01")]
    public async Task Kick_OfSomeoneRankedAboveTheModerator_IsRefused()
    {
        this.Guild.SetRoles(this.Guild.Moderator, this.Guild.ModeratorRoleId, this.Guild.KickerRoleId);

        await this.KickAsync(this.Guild.Moderator, this.Guild.Admin);

        await this.ShouldHaveNoSinsOrKicksAsync();
    }

    /// <summary>MOD-54.</summary>
    [Fact]
    public async Task Kick_WhenTheBotLacksKickMembers_IsRefused()
    {
        await this._bot.DisposeAsync();
        this._bot = await StartBotAsync(this._database, guild => guild.BotPermissions =
            FakeGuild.ViewChannelPermission | FakeGuild.SendMessagesPermission | FakeGuild.EmbedLinksPermission);
        await this._bot.ConfigureModerationAsync();

        (await this.KickAsync(this.Guild.Admin, this.Guild.User1)).Description
            .ShouldBe("The bot did not have the needed permissions to execute this command.");

        await this.ShouldHaveNoSinsOrKicksAsync();
    }

    /// <summary>MOD-55.</summary>
    [Fact]
    public async Task Kick_WithModerationDisabled_IsRefused()
    {
        await this._bot.SetModuleAsync(Module.Moderation, false);

        (await this.KickAsync(this.Guild.Admin, this.Guild.User1)).Description.ShouldBe("This module is disabled in this server.");

        await this.ShouldHaveNoSinsOrKicksAsync();
    }

    private Task<FakeReply> KickAsync(FakeUser moderator, FakeUser target, string? reason = null)
        => this._bot.RunSlashCommandAndWaitAsync("kick", moderator,
            reason is null
                ? [SlashOption.User("member", target)]
                : [SlashOption.User("member", target), SlashOption.String("reason", reason)]);

    private Func<RecordedRequest, bool> IsKickOf(FakeUser target)
        => request => request.Method == HttpMethod.Delete && request.Path == $"guilds/{this.Guild.Id}/members/{target.Id}";

    private async Task<Sin> SingleSinAsync()
    {
        await using var dbContext = this._database.CreateDbContext();
        return await dbContext.Sins.Include(sin => sin.ReasonHistory).SingleAsync(TestContext.Current.CancellationToken);
    }

    private async Task ShouldHaveNoSinsOrKicksAsync()
    {
        await using var dbContext = this._database.CreateDbContext();
        (await dbContext.Sins.AnyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
        this._bot.Rest.Kicked.ShouldBeEmpty();
    }

    private static string Field(JsonNode embed, string name)
        => embed["fields"]!.AsArray().Single(field => field!["name"]!.GetValue<string>() == name)!["value"]!.GetValue<string>();
}
