// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using Grimoire.Tests.Integration.FakeDiscord;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Tests.Integration.CustomCommands;

/// <summary>
///     <c>/Commands Learn</c> cases past what <see cref="CustomCommandSettingsTests" /> covers (backlog A-48, area 07
///     section A), and the deny roles decided in review C-2.
/// </summary>
[Collection("Test collection")]
public sealed class CustomCommandLearnGapTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private readonly GrimoireCoreFactory _database = database;
    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await this._database.ResetDatabase();
        this._bot = await GrimoireTestBot.StartAsync(this._database);
        await this._bot.ConfigureModerationAsync();
        await this._bot.SetModuleAsync(Module.Commands, true);
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    /// <summary>CC-04: a leading <c>#</c> on the colour is dropped.</summary>
    [Fact]
    public async Task AColourWithAHash_IsStoredWithoutIt()
    {
        (await this.LearnAsync("rules", "Be nice", SlashOption.Boolean("embed", true), SlashOption.String("embed_color", "#00FF00")))
            .Description.ShouldBe("Added rules custom command.");

        (await this.VersionsAsync("rules")).ShouldHaveSingleItem().ShouldBeOfType<EmbedCustomCommand>()
            .EmbedColor!.Value.Value.ShouldBe("00FF00");
        this._bot.Rest.Unhandled.ShouldBeEmpty();
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>CC-10: a role given twice is saved once.</summary>
    [Fact]
    public async Task ARoleGivenTwice_IsSavedOnce()
    {
        await this.LearnAsync("secret", "shh", SlashOption.Boolean("restricted_use", true),
            SlashOption.Role("permission_role_1", this.Guild.KickerRoleId), SlashOption.Role("permission_role_3", this.Guild.KickerRoleId));

        (await this.VersionsAsync("secret")).ShouldHaveSingleItem().Roles.ShouldHaveSingleItem()
            .ShouldBeOfType<CustomCommandAllowRole>().RoleId.ShouldBe(new RoleId(this.Guild.KickerRoleId));
    }

    /// <summary>
    ///     CC-09, as decided in review C-2: roles given without <c>restricted_use</c> become deny roles, so members
    ///     with them can't use the command and everyone else can.
    /// </summary>
    [Fact(Skip = "Fails: BuildRoles only makes allow roles, so the role is dropped and everyone can use it. review C-2")]
    public async Task RolesWithoutRestrictedUse_BecomeDenyRoles()
    {
        await this.LearnAsync("open", "hi", SlashOption.Role("permission_role_1", this.Guild.ModeratorRoleId));

        (await this.VersionsAsync("open")).ShouldHaveSingleItem().Roles.ShouldHaveSingleItem()
            .ShouldBeOfType<CustomCommandDenyRole>().RoleId.ShouldBe(new RoleId(this.Guild.ModeratorRoleId));
        var denied = this._bot.RunSlashCommand("command", this.Guild.Moderator, SlashOption.String("command_name", "open"));
        await this._bot.Rest.WaitForRequestAsync(denied.IsOriginalResponseDelete);
        (await this._bot.RunSlashCommandAndWaitAsync("command", this.Guild.User1, SlashOption.String("command_name", "open")))
            .Message["content"]!.GetValue<string>().ShouldBe("hi");
    }

    /// <summary>CC-11: learning an existing name should say it was updated, not added.</summary>
    [Fact(Skip = "Fails: Learn always replies \"Added {name} custom command.\". KI-88")]
    public async Task LearningAnExistingName_SaysUpdated()
    {
        await this.LearnAsync("hello", "first");

        (await this.LearnAsync("hello", "second")).Description.ShouldNotBe("Added hello custom command.");
    }

    /// <summary>CC-12: each version carries its own roles, so learning without them opens the command to all.</summary>
    [Fact]
    public async Task ANewVersionWithoutRoles_OpensTheCommand()
    {
        await this.LearnAsync("secret", "shh", SlashOption.Boolean("restricted_use", true),
            SlashOption.Role("permission_role_1", this.Guild.ModeratorRoleId));
        await this.LearnAsync("secret", "open now");

        var versions = await this.VersionsAsync("secret");
        versions.Count.ShouldBe(2);
        versions.MaxBy(version => version.CreatedAt)!.Roles.ShouldBeEmpty();
        versions.MinBy(version => version.CreatedAt)!.Roles.ShouldHaveSingleItem();
        (await this._bot.RunSlashCommandAndWaitAsync("command", this.Guild.User1, SlashOption.String("command_name", "secret")))
            .Message["content"]!.GetValue<string>().ShouldBe("open now");
        await GrimoireTestBot.EventuallyAsync(this.UseCountAsync, count => count == 1);
    }

    /// <summary>CC-15 and CC-16: blank names and blank content are refused with a reason.</summary>
    [Theory]
    [InlineData("   ", "hi", "A value for command name must be provided.")]
    [InlineData("hello", "   ", "Command content cannot be empty.")]
    [InlineData("hello", @"\n", "Command content cannot be empty.")]
    public async Task BlankNameOrContent_IsRefused(string name, string content, string error)
    {
        var reply = await this.LearnAsync(name, content);

        reply.Description.ShouldBe(error);
        reply.IsEphemeral.ShouldBeTrue();
        await using var dbContext = this._database.CreateDbContext();
        (await dbContext.CustomCommands.AnyAsync(Token)).ShouldBeFalse();
    }

    /// <summary>CC-18: only <c>\n</c> is unescaped, so a Windows path is stored as typed (review C-5).</summary>
    [Fact]
    public async Task ABackslashPath_IsStoredAsTyped()
    {
        await this.LearnAsync("path", @"C:\temp\files");

        (await this.VersionsAsync("path")).ShouldHaveSingleItem().Content.Value.ShouldBe(@"C:\temp\files");
    }

    /// <summary>CC-19: names are case-sensitive, so these are two commands. KI-87 asks whether that's wanted.</summary>
    [Fact]
    public async Task NamesDifferingOnlyInCase_AreSeparateCommands()
    {
        await this.LearnAsync("Hello", "upper");
        await this.LearnAsync("hello", "lower");

        (await this.VersionsAsync("Hello")).ShouldHaveSingleItem().Content.Value.ShouldBe("upper");
        (await this.VersionsAsync("hello")).ShouldHaveSingleItem().Content.Value.ShouldBe("lower");
    }

    /// <summary>CC-23: the owner and an Administrator pass the Manage Guild gate.</summary>
    [Theory]
    [InlineData("owner")]
    [InlineData("admin")]
    public async Task TheOwnerAndAnAdministrator_CanLearn(string who)
    {
        var invoker = who == "owner" ? this.Guild.Owner : this.Guild.Admin;

        (await this._bot.RunSlashCommandAndWaitAsync("commands", invoker, SlashOption.Subcommand("learn",
                SlashOption.String("name", "hello"), SlashOption.String("content", "hi"))))
            .Description.ShouldBe("Added hello custom command.");

        (await this.VersionsAsync("hello")).ShouldHaveSingleItem().ModeratorId.ShouldBe(new ModeratorId(invoker.Id));
    }

    /// <summary>CC-25: with no mod log set, Learn still works and posts nowhere.</summary>
    [Fact]
    public async Task WithoutAModLog_LearnStillWorks()
    {
        (await this._bot.Services.GetRequiredService<SettingsModule>().SetLogChannelSetting(GuildLogType.Moderation,
            new GuildId(this.Guild.Id), new ModeratorId(this.Guild.Owner.Id), null, cancellationToken: TestContext.Current.CancellationToken)).ShouldBeSetOrUnchanged();

        (await this.LearnAsync("hello", "hi")).Description.ShouldBe("Added hello custom command.");

        (await this.VersionsAsync("hello")).ShouldHaveSingleItem();
        (await this._bot.Rest.ReceivesRequestWithinAsync(request => request.Method == HttpMethod.Post
                                                                    && request.Path.StartsWith("channels/", StringComparison.Ordinal)
                                                                    && request.Path.EndsWith("/messages", StringComparison.Ordinal),
            TimeSpan.FromSeconds(2))).ShouldBeFalse();
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    private Task<FakeReply> LearnAsync(string name, string content, params SlashOption[] options)
        => this._bot.RunSlashCommandAndWaitAsync("commands", this.Guild.Admin, SlashOption.Subcommand("learn",
            [SlashOption.String("name", name), SlashOption.String("content", content), .. options]));

    private async Task<List<CustomCommand>> VersionsAsync(string name)
    {
        await using var dbContext = this._database.CreateDbContext();
        var commandName = CustomCommandName.Create(name).ShouldSucceed();
        return await dbContext.CustomCommands.AsNoTracking().Include(command => command.Roles)
            .Where(command => command.GuildId == new GuildId(this.Guild.Id) && command.Name == commandName).ToListAsync(Token);
    }

    private async Task<int> UseCountAsync()
    {
        await using var dbContext = this._database.CreateDbContext();
        return await dbContext.CustomCommandUsages.CountAsync(Token);
    }
}
