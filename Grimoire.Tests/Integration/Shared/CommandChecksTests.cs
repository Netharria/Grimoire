// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings.Enums;
using Grimoire.Tests.Integration.FakeDiscord;

namespace Grimoire.Tests.Integration.Shared;

/// <summary>
///     The shared context checks, section A of <c>docs/testing/01-general-settings.md</c>. A failed check is answered
///     straight away with an ephemeral error, before the command defers. GEN-08 is covered with <c>/UserInfo</c>
///     (A-03), and GEN-16 with multi-guild isolation (A-35).
/// </summary>
[Collection("Test collection")]
public sealed class CommandChecksTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private const string PermissionError = "You do not have the required permissions to use this command.";
    private const string ModuleDisabledError = "This module is disabled in this server.";

    private readonly GrimoireCoreFactory _database = database;
    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;

    public async ValueTask InitializeAsync()
    {
        await this._database.ResetDatabase();
        this._bot = await GrimoireTestBot.StartAsync(this._database);
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    private static void ShouldBeModulesView(FakeReply reply)
    {
        var title = reply.Embed?["author"]?["name"]?.GetValue<string>();
        title.ShouldBe("Current states of modules.", reply.Message.ToJsonString());
    }

    private Task<FakeReply> ViewModulesAs(FakeUser user)
        => this._bot.RunSlashCommandAndWaitAsync("modules", user, SlashOption.Subcommand("view"));

    /// <summary>GEN-01: the owner holds no role with ManageGuild, but the owner is always allowed.</summary>
    [Fact]
    public async Task Owner_IsAllowed_WithoutAnyRole()
        => ShouldBeModulesView(await this.ViewModulesAs(this.Guild.Owner));

    /// <summary>GEN-02.</summary>
    [Fact]
    public async Task Administrator_IsAllowed_WithoutExplicitManageGuild()
        => ShouldBeModulesView(await this.ViewModulesAs(this.Guild.Admin));

    /// <summary>GEN-03 and GEN-04.</summary>
    [Theory]
    [InlineData("mod")]
    [InlineData("user1")]
    public async Task MemberWithoutManageGuild_GetsAnEphemeralPermissionError(string who)
    {
        var reply = await this.ViewModulesAs(who == "mod" ? this.Guild.Moderator : this.Guild.User1);

        reply.Description.ShouldBe(PermissionError);
        reply.IsEphemeral.ShouldBeTrue();
        reply.WasDeferred.ShouldBeFalse("checks run before the command body defers");
    }

    /// <summary>GEN-05 and GEN-06: an exact ManageGuild role is enough, and permissions are read live.</summary>
    [Fact]
    public async Task ManageGuildRole_Allows_AndRemovingItDeniesAgain()
    {
        this.Guild.SetRoles(this.Guild.User1, this.Guild.ManagerRoleId);
        ShouldBeModulesView(await this.ViewModulesAs(this.Guild.User1));

        this.Guild.SetRoles(this.Guild.User1);
        (await this.ViewModulesAs(this.Guild.User1)).Description.ShouldBe(PermissionError);
    }

    /// <summary>GEN-07: no command is offered in DMs, so Discord never sends one from a DM.</summary>
    [Fact]
    public void NoCommand_IsAvailableInDirectMessages()
        => this._bot.Rest.RegisteredCommands
            .Where(command => command.Value["dm_permission"]?.GetValue<bool>() != false)
            .Select(command => command.Key)
            .ShouldBeEmpty();

    /// <summary>GEN-10 to GEN-14: one command per gated module, run by an admin so only the module check fails.</summary>
    [Theory]
    [InlineData("moderation")]
    [InlineData("leveling")]
    [InlineData("commands")]
    [InlineData("message log")]
    [InlineData("user log")]
    public async Task CommandInADisabledModule_GetsTheModuleDisabledError(string module)
    {
        var (command, options) = module switch
        {
            "moderation" => ("ban", new[] { SlashOption.User("user", this.Guild.User1) }),
            "leveling" => ("level", Array.Empty<SlashOption>()),
            "commands" => ("commands", [SlashOption.Subcommand("stats", SlashOption.String("name", "hello"))]),
            "message log" => ("log", [SlashOption.Group("message", SlashOption.Subcommand("view"))]),
            "user log" => ("log", [SlashOption.Group("user", SlashOption.Subcommand("view"))]),
            _ => throw new ArgumentOutOfRangeException(nameof(module), module, null)
        };

        var reply = await this._bot.RunSlashCommandAndWaitAsync(command, this.Guild.Admin, options);

        reply.Description.ShouldBe(ModuleDisabledError);
        reply.IsEphemeral.ShouldBeTrue();
    }

    /// <summary>GEN-10 and GEN-15: enabling a module takes effect straight away, with no restart.</summary>
    [Fact]
    public async Task EnablingAModule_UnblocksItsCommandsImmediately()
    {
        await this._bot.SetModuleAsync(Module.Moderation, false);
        var warn = new[] { SlashOption.User("user", this.Guild.User1), SlashOption.String("reason", "test") };
        (await this._bot.RunSlashCommandAndWaitAsync("warn", this.Guild.Moderator, warn))
            .Description.ShouldBe(ModuleDisabledError);

        await this._bot.SetModuleAsync(Module.Moderation, true);

        var reply = await this._bot.RunSlashCommandAndWaitAsync("warn", this.Guild.Moderator, warn);
        reply.Embed!["author"]!["name"]!.GetValue<string>().ShouldBe("Warn");
    }

    /// <summary>GEN-17 and GEN-86: <c>/Modules</c> is not module gated, so it can't lock a guild out.</summary>
    [Fact]
    public async Task ModulesCommands_WorkWithEveryModuleDisabled()
    {
        foreach (var module in new[] { Module.Leveling, Module.UserLog, Module.Moderation, Module.MessageLog, Module.Commands, Module.AntiSpam })
            await this._bot.SetModuleAsync(module, false);

        ShouldBeModulesView(await this.ViewModulesAs(this.Guild.Admin));
        (await this._bot.RunSlashCommandAndWaitAsync("modules", this.Guild.Admin, SlashOption.Subcommand("set",
                SlashOption.Enum("module", Grimoire.Features.Shared.Commands.ModuleCommands.ModuleArguments.Moderation),
                SlashOption.Boolean("enable", true))))
            .Description.ShouldBe("Enabled Moderation");
    }
}
