// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings.Domain;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using Grimoire.Tests.Integration.FakeDiscord;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Tests.Integration.Leveling;

/// <summary>
///     <c>/Ignore</c> cases from <c>06-leveling.md</c> section G that <see cref="IgnoreAndAwardTests" /> doesn't cover
///     (backlog A-47): the option names, repeats, removing what wasn't ignored, the empty View headings and the module
///     gate.
/// </summary>
[Collection("Test collection")]
public sealed class IgnoreCommandTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;
    private SettingsModule Settings => this._bot.Services.GetRequiredService<SettingsModule>();
    private GuildId GuildId => new(this.Guild.Id);
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await database.ResetDatabase();
        this._bot = await GrimoireTestBot.StartAsync(database);
        await this._bot.ConfigureModerationAsync();
        await this._bot.SetModuleAsync(Module.Leveling, true);
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    /// <summary>
    ///     KI-82: Add and Remove register their user and role list under the same name, <c>items_0</c> to
    ///     <c>items_23</c>, read from the registration the bot sends at startup.
    /// </summary>
    [Fact]
    public void AddAndRemove_NameTheirListTheSame()
    {
        var add = this.MentionableOptionNames("add");
        var remove = this.MentionableOptionNames("remove");

        add.ShouldBe(Enumerable.Range(0, 24).Select(i => $"items_{i}"));
        remove.ShouldBe(add);
    }

    /// <summary>LVL-123, review X-12: ignoring someone already ignored says so and adds no row.</summary>
    [Fact]
    public async Task Add_SomeoneAlreadyIgnored_SaysSo_AndAddsNoRow()
    {
        (await this.AddAsync(this.Guild.User1)).Description.ShouldBe($"<@{this.Guild.User1.Id}> are now ignored for xp gain.");

        (await this.AddAsync(this.Guild.User1)).Description.ShouldBe($"<@{this.Guild.User1.Id}> were already ignored.");

        (await this.TrackedRowsAsync(this.Guild.User1.Id)).ShouldHaveSingleItem().ShouldBeOfType<IgnoredMember>();
        (await this.Settings.GetAllIgnoredItems(this.GuildId, cancellationToken: TestContext.Current.CancellationToken)).ShouldSucceed().ShouldHaveSingleItem();
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>LVL-126, review X-12: removing someone who wasn't ignored says so and records nothing.</summary>
    [Fact]
    public async Task Remove_SomeoneNotIgnored_SaysSo_AndRecordsNothing()
    {
        var reply = await this._bot.RunSlashCommandAndWaitAsync("ignore", this.Guild.Admin,
            SlashOption.Subcommand("remove", SlashOption.MentionableUser("items_0", this.Guild.Moderator)));

        reply.Description.ShouldBe($"<@{this.Guild.Moderator.Id}> were not ignored.");
        (await this.TrackedRowsAsync(this.Guild.Moderator.Id)).ShouldBeEmpty();
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>LVL-129: with only users ignored, the Channels and Roles headings are still shown, empty.</summary>
    [Fact]
    public async Task View_WithOnlyUsers_StillShowsTheEmptyHeadings()
    {
        await this.AddAsync(this.Guild.User1);

        var reply = await this._bot.RunSlashCommandAndWaitAsync("ignore", this.Guild.Admin, SlashOption.Subcommand("view"));

        reply.Description.ShouldBe($"**Channels**\n\n\n**Roles**\n\n\n**Users**\n<@{this.Guild.User1.Id}>");
    }

    /// <summary>LVL-133: with Leveling off, Add, Remove and View are refused.</summary>
    [Theory]
    [InlineData("add")]
    [InlineData("remove")]
    [InlineData("view")]
    public async Task WithLevelingDisabled_IsRefused(string subcommand)
    {
        await this._bot.SetModuleAsync(Module.Leveling, false);

        var reply = await this._bot.RunSlashCommandAndWaitAsync("ignore", this.Guild.Admin, subcommand == "view"
            ? SlashOption.Subcommand("view")
            : SlashOption.Subcommand(subcommand, SlashOption.MentionableUser("items_0", this.Guild.User1)));

        reply.Description.ShouldBe("This module is disabled in this server.");
        (await this.TrackedRowsAsync(this.Guild.User1.Id)).ShouldBeEmpty();
    }

    private Task<FakeReply> AddAsync(FakeUser user)
        => this._bot.RunSlashCommandAndWaitAsync("ignore", this.Guild.Admin,
            SlashOption.Subcommand("add", SlashOption.MentionableUser("items_0", user)));

    private string[] MentionableOptionNames(string subcommand)
        => [.. this._bot.Rest.RegisteredCommands["ignore"]["options"]!.AsArray()
            .Single(option => option!["name"]!.GetValue<string>() == subcommand)!["options"]!.AsArray()
            .Where(option => option!["type"]!.GetValue<int>() == 9)
            .Select(option => option!["name"]!.GetValue<string>())];

    private async Task<List<XpTrackedItem>> TrackedRowsAsync(ulong id)
    {
        await using var settings = database.CreateSettingsDbContext();
        return (await settings.XpTrackedItems.AsNoTracking().Where(item => item.GuildId == this.GuildId).ToListAsync(Token))
            .Where(item => item switch
            {
                IgnoredMember member => member.UserId.Value == id,
                WatchedMember member => member.UserId.Value == id,
                _ => false
            })
            .ToList();
    }
}
