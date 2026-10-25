// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text.Json.Nodes;
using Grimoire.Settings.Enums;
using Grimoire.Tests.Integration.FakeDiscord;

namespace Grimoire.Tests.Integration.Moderation;

/// <summary>
///     <c>/Pardon</c>, <c>/Forget</c> and <c>/Reason</c>, cases SIN-20 to SIN-43 in <c>docs/testing/03-sins.md</c>.
///     These find a sin through the user's newest username, so each test seeds one. SIN-23's other guild is stood in
///     for by a sin with another guild id.
/// </summary>
[Collection("Test collection")]
public sealed class SinAdminTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private const string NotFound = "Could not find a sin with that ID.";

    private readonly GrimoireCoreFactory _database = database;
    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;
    private GuildId GuildId => new(this.Guild.Id);

    public async ValueTask InitializeAsync()
    {
        await this._database.ResetDatabase();
        this._bot = await GrimoireTestBot.StartAsync(this._database);
        await this._bot.ConfigureModerationAsync();
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    // ── /Pardon ───────────────────────────────────────────────────────────────

    /// <summary>SIN-20.</summary>
    [Fact]
    public async Task Pardon_RecordsIt_RepliesAndLogs()
    {
        var sin = await this.SeedWarnAsync(this.Guild.User1);

        var reply = await this.PardonAsync(sin.Id, "forgiven");

        reply.Embed!["author"]!["name"]!.GetValue<string>().ShouldBe("Pardoned");
        reply.Description.ShouldBe($"**ID:** {sin.Id} **User:** user1");
        await using var dbContext = this._database.CreateDbContext();
        var pardon = await dbContext.Pardons.SingleAsync(TestContext.Current.CancellationToken);
        pardon.SinId.ShouldBe(sin.Id);
        pardon.ModeratorId.ShouldBe(new ModeratorId(this.Guild.Moderator.Id));
        pardon.Reason.Value.ShouldBe("forgiven");
        var logged = await this._bot.WaitForModLogEmbedAsync(e => e["author"]?["name"]?.GetValue<string>() == "Pardon");
        Fields(logged).ShouldBe([("User", "user1"), ("Sin Id", sin.Id.ToString()), ("Moderator", this.Guild.Moderator.MemberMention), ("Reason", "forgiven")]);
        this._bot.Rest.Unhandled.ShouldBeEmpty();
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>SIN-21.</summary>
    [Fact(Skip = "Fails: with no reason the command creates a ModerationReason from \"\", which is invalid, and throws. KI-32")]
    public async Task Pardon_WithoutAReason_StillPardons()
    {
        var sin = await this.SeedWarnAsync(this.Guild.User1);

        (await this._bot.RunSlashCommandAndWaitAsync("pardon", this.Guild.Moderator, SlashOption.String("sin_id", sin.Id.ToString())))
            .Embed!["author"]!["name"]!.GetValue<string>().ShouldBe("Pardoned");
    }

    /// <summary>SIN-22 and SIN-23: an unknown id, or one from another guild.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Pardon_OfASinNotInThisGuild_IsNotFound(bool otherGuild)
    {
        var id = otherGuild
            ? (await this.SeedWarnAsync(this.Guild.User1, new GuildId(1_100_000_000_000_000_999))).Id
            : new SinId(999_999);

        (await this.PardonAsync(id, "x")).Description.ShouldBe(NotFound);
        await using var dbContext = this._database.CreateDbContext();
        (await dbContext.Pardons.AnyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    /// <summary>SIN-24.</summary>
    [Fact(Skip = "Fails: the username lookup doubles as the existence check, so a user with no username history looks " +
                 "like a missing sin. KI-33")]
    public async Task Pardon_ForAUserWithNoUsernameHistory_StillPardons()
    {
        var sin = await this.SeedWarnAsync(new FakeUser(1_100_000_000_000_000_095, "stranger"), withUsername: false);

        (await this.PardonAsync(sin.Id, "x")).Description.ShouldNotBe(NotFound);
    }

    /// <summary>SIN-25: there's no "already pardoned" check, so a second pardon adds a second row.</summary>
    [Fact]
    public async Task Pardon_Twice_AddsASecondPardon()
    {
        var sin = await this.SeedWarnAsync(this.Guild.User1);

        await this.PardonAsync(sin.Id, "first");
        await this.PardonAsync(sin.Id, "second");

        await using var dbContext = this._database.CreateDbContext();
        (await dbContext.Pardons.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(2);
    }

    /// <summary>SIN-26: an id that isn't a positive number is rejected before anything happens.</summary>
    [Theory]
    [InlineData("abc")]
    [InlineData("0")]
    public async Task Pardon_WithAnInvalidId_IsRejected(string sinId)
    {
        var reply = await this._bot.RunSlashCommandAndWaitAsync("pardon", this.Guild.Moderator,
            SlashOption.String("sin_id", sinId), SlashOption.String("reason", "x"));

        reply.Embed!["author"]?["name"]?.GetValue<string>().ShouldNotBe("Pardoned");
        await using var dbContext = this._database.CreateDbContext();
        (await dbContext.Pardons.AnyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    /// <summary>SIN-27 and SIN-28: each command needs Manage Messages and the Moderation module.</summary>
    [Theory]
    [InlineData("pardon", "permission")]
    [InlineData("forget", "permission")]
    [InlineData("reason", "permission")]
    [InlineData("pardon", "module")]
    [InlineData("forget", "module")]
    [InlineData("reason", "module")]
    public async Task SinCommands_NeedManageMessagesAndTheModule(string command, string missing)
    {
        var sin = await this.SeedWarnAsync(this.Guild.User1);
        if (missing == "module")
            await this._bot.SetModuleAsync(Module.Moderation, false);
        SlashOption[] options = command == "forget"
            ? [SlashOption.String("sin_id", sin.Id.ToString())]
            : [SlashOption.String("sin_id", sin.Id.ToString()), SlashOption.String("reason", "x")];

        var reply = await this._bot.RunSlashCommandAndWaitAsync(command, missing == "module" ? this.Guild.Moderator : this.Guild.User1, options);

        reply.Description.ShouldBe(missing == "module"
            ? "This module is disabled in this server."
            : "You do not have the required permissions to use this command.");
        await using var dbContext = this._database.CreateDbContext();
        (await dbContext.Sins.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(1);
        (await dbContext.Pardons.AnyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    /// <summary>SIN-43: Discord's client enforces the reason length.</summary>
    [Theory]
    [InlineData("pardon")]
    [InlineData("reason")]
    public void Reasons_AreRegisteredWithAThousandCharacterLimit(string command)
        => this._bot.Rest.RegisteredCommands[command]["options"]!.AsArray()
            .Single(option => option!["name"]!.GetValue<string>() == "reason")!["max_length"]!.GetValue<int>().ShouldBe(1000);

    // ── /Forget ───────────────────────────────────────────────────────────────

    /// <summary>SIN-30 and SIN-32: the sin goes, with its reasons and pardons, and the mod log is told.</summary>
    [Fact]
    public async Task Forget_DeletesTheSinWithItsReasonsAndPardons()
    {
        var sin = await this.SeedWarnAsync(this.Guild.User1);
        await this.PardonAsync(sin.Id, "forgiven");
        await this._bot.RunSlashCommandAndWaitAsync("reason", this.Guild.Moderator,
            SlashOption.String("sin_id", sin.Id.ToString()), SlashOption.String("reason", "changed"));

        var reply = await this._bot.RunSlashCommandAndWaitAsync("forget", this.Guild.Moderator, SlashOption.String("sin_id", sin.Id.ToString()));

        reply.Embed!["author"]!["name"]!.GetValue<string>().ShouldBe("Forgot");
        reply.Description.ShouldBe($"**ID:** {sin.Id} **User:** user1");
        await using var dbContext = this._database.CreateDbContext();
        (await dbContext.Sins.AnyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await dbContext.Pardons.AnyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await dbContext.SinReasonHistory.AnyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
        var logged = await this._bot.WaitForModLogEmbedAsync(e =>
            e["author"]?["name"]?.GetValue<string>()?.EndsWith("has been commanded to forget.", StringComparison.Ordinal) == true);
        Fields(logged).ShouldBe([("User", "user1"), ("Sin Id", sin.Id.ToString()), ("Moderator", this.Guild.Moderator.MemberMention)]);
    }

    /// <summary>SIN-31.</summary>
    [Fact]
    public async Task Forget_OfAnUnknownSin_IsNotFound()
        => (await this._bot.RunSlashCommandAndWaitAsync("forget", this.Guild.Moderator, SlashOption.String("sin_id", "999999")))
            .Description.ShouldBe(NotFound);

    // ── /Reason ───────────────────────────────────────────────────────────────

    /// <summary>SIN-40: a new reason is recorded, shown, and is what /SinLog uses from then on.</summary>
    [Fact]
    public async Task Reason_RecordsTheNewReason_RepliesAndLogs()
    {
        var sin = await this.SeedWarnAsync(this.Guild.User1);

        var reply = await this._bot.RunSlashCommandAndWaitAsync("reason", this.Guild.Moderator,
            SlashOption.String("sin_id", sin.Id.ToString()), SlashOption.String("reason", "updated"));

        reply.Embed!["author"]!["name"]!.GetValue<string>().ShouldBe("Reason Updated");
        Fields(reply.Embed!).ShouldBe([("Id", sin.Id.ToString()), ("User", "user1"), ("Reason", "updated")]);
        await using var dbContext = this._database.CreateDbContext();
        var history = await dbContext.SinReasonHistory.SingleAsync(TestContext.Current.CancellationToken);
        history.Reason.Value.ShouldBe("updated");
        history.Actor.ShouldBe(new ModerationActor.Moderator(new ModeratorId(this.Guild.Moderator.Id)));
        await this._bot.WaitForModLogEmbedAsync(e => e["description"]?.GetValue<string>()
            == $"{this.Guild.Moderator.MemberMention} updated reason to updated for **ID:** {sin.Id} **User:** user1");
        (await this._bot.RunSlashCommandAndWaitAsync("sin_log", this.Guild.Moderator,
                SlashOption.Integer("type", 4), SlashOption.User("user", this.Guild.User1)))
            .Description.ShouldNotBeNull().ShouldContain("Reason: updated");
    }

    /// <summary>SIN-41.</summary>
    [Fact]
    public async Task Reason_ForAnUnknownSin_IsNotFound()
        => (await this._bot.RunSlashCommandAndWaitAsync("reason", this.Guild.Moderator,
                SlashOption.String("sin_id", "999999"), SlashOption.String("reason", "x")))
            .Description.ShouldBe(NotFound);

    /// <summary>SIN-42.</summary>
    [Fact(Skip = "Fails: as SIN-24, a user with no username history looks like a missing sin. KI-33")]
    public async Task Reason_ForAUserWithNoUsernameHistory_StillUpdates()
    {
        var sin = await this.SeedWarnAsync(new FakeUser(1_100_000_000_000_000_095, "stranger"), withUsername: false);

        (await this._bot.RunSlashCommandAndWaitAsync("reason", this.Guild.Moderator,
                SlashOption.String("sin_id", sin.Id.ToString()), SlashOption.String("reason", "x")))
            .Description.ShouldNotBe(NotFound);
    }

    private Task<FakeReply> PardonAsync(SinId sinId, string reason)
        => this._bot.RunSlashCommandAndWaitAsync("pardon", this.Guild.Moderator,
            SlashOption.String("sin_id", sinId.ToString()), SlashOption.String("reason", reason));

    /// <summary>Seeds a warn and, unless told not to, a username newer than the startup sync's.</summary>
    private async Task<Sin> SeedWarnAsync(FakeUser user, GuildId? guildId = null, bool withUsername = true)
    {
        var sin = Sin.ForWarn(new ModerationActor.Moderator(new ModeratorId(this.Guild.Admin.Id)), new UserId(user.Id),
            guildId ?? this.GuildId, DateTimeOffset.UtcNow.AddMinutes(-10)).ShouldSucceed();
        await using var seed = this._database.CreateDbContext();
        seed.Sins.Add(sin);
        if (withUsername)
            seed.UsernameHistory.Add(new UsernameHistory
            {
                UserId = new UserId(user.Id),
                Username = Username.Create(user.Username).ShouldSucceed(),
                Timestamp = DateTimeOffset.UtcNow.AddMinutes(1)
            });
        await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        return sin;
    }

    private static (string, string)[] Fields(JsonNode embed)
        => [.. embed["fields"]!.AsArray().Select(field => (field!["name"]!.GetValue<string>(), field["value"]!.GetValue<string>()))];
}
