// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Entities;
using Grimoire.Features.Shared.GuildIssues;
using Grimoire.Functional.Boundary;
using Grimoire.Functional.Runtime;
using Grimoire.UnitTests.Functional.Fakes;
using LanguageExt;
using Unit = LanguageExt.Unit;

namespace Grimoire.UnitTests.Functional.Discord;

/// <summary>Spec 1.4, section 3.7: a permission Discord refused the bot becomes a guild issue, once.</summary>
public sealed class BotRefusalTests
{
    private static readonly GuildId Guild = new(10);
    private static readonly ChannelId Channel = new(20);
    private readonly TestEnv _env = new();

    private static Invocation Slash { get; } = new("Probe", InvocationKind.Slash, Guild, Channel,
        new UserId(30), DiscordPermissions.None, DateTimeOffset.UnixEpoch);

    private static Forbidden Refused => FakeDiscord.MissingPermission(Guild, "Manage Messages", Channel,
        "delete that message");

    private Task<Fin<CommandOutcome>> RunCommand(Error error) =>
        this._env.Run(CommandBoundary<CommandRuntime>.Run(Slash, Eff<CommandRuntime, Privacy>.Pure(Privacy.Public),
            _ => Success<Error, int>(1), _ => Eff<CommandRuntime, Reply>.Fail(error)));

    private Task<Fin<CommandOutcome>> RunEvent(Error error) =>
        this._env.Run(EventBoundary<GrimoireRuntime>.Run("ProbeHandler", Guild, Eff<GrimoireRuntime, Unit>.Fail(error)));

    [Fact]
    public async Task ACommandRefusedByDiscord_IsRefused_AndTheGuildIsTold()
    {
        (await this.RunCommand(Refused)).ShouldSucceed().ShouldBe(CommandOutcome.Forbidden);

        this._env.Command.Calls.OfType<CommandCall.Refused>().ShouldHaveSingleItem();
        var issue = this._env.GuildIssues.Reported.ShouldHaveSingleItem();
        issue.GuildId.ShouldBe(Guild);
        issue.Code.ShouldBe("bot-missing-permission:Manage Messages:20");
    }

    [Fact]
    public async Task ACommandRefusedByOurOwnRules_TellsTheGuildNothing()
    {
        await this.RunCommand(new Forbidden("Only moderators can do that."));

        this._env.GuildIssues.Reported.ShouldBeEmpty();
    }

    [Fact]
    public async Task TheSameRefusalTwiceInOneError_IsReportedOnce()
    {
        await this.RunCommand(Error.Many(Refused, Refused));

        this._env.GuildIssues.Reported.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task ARefusalInsideAnOutage_IsStillReported()
    {
        (await this.RunCommand(Error.Many(Refused, FakeDiscord.Outage()))).ShouldSucceed()
            .ShouldBe(CommandOutcome.Unavailable);

        this._env.GuildIssues.Reported.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task AFailedReport_LeavesTheOutcomeAsItWas()
    {
        this._env.GuildIssues.Fails = FakeDiscord.Outage();

        (await this.RunCommand(Refused)).ShouldSucceed().ShouldBe(CommandOutcome.Forbidden);

        this._env.Log.Entries.ShouldContain(entry => entry.Template.StartsWith("Could not report"));
    }

    [Fact]
    public async Task AnEventRefusedByDiscord_TellsTheGuild()
    {
        (await this.RunEvent(Refused)).ShouldSucceed().ShouldBe(CommandOutcome.Forbidden);

        this._env.GuildIssues.Reported.ShouldHaveSingleItem().GuildId.ShouldBe(Guild);
        this._env.Alerts.Sent.ShouldHaveSingleItem().Type.ShouldBe("EventHandlerFailure");
    }

    [Fact]
    public async Task AnEventThatFailsWithoutARefusal_TellsTheGuildNothing()
    {
        await this.RunEvent(new NotFound("gone"));

        this._env.GuildIssues.Reported.ShouldBeEmpty();
    }
}

public sealed class BotMissingPermissionIssueTests
{
    private static readonly GuildId Guild = new(10);

    [Fact]
    public void InAChannel_ItNamesTheChannelAndItsOverwrites()
    {
        var issue = GuildIssueCatalog.BotMissingPermission(
            new BotRefusal(Guild, new ChannelId(20), "Manage Messages", "delete that message"));

        issue.Code.ShouldBe("bot-missing-permission:Manage Messages:20");
        issue.Summary.ShouldBe("Discord refused to let me delete that message in <#20>: I don't have **Manage Messages**.");
        issue.SuggestedFix.ShouldBe(
            "Give my role **Manage Messages** in that channel, and check its permission overwrites.");
    }

    [Fact]
    public void AnOperationThatNamesItsChannel_DoesntRepeatIt()
        => GuildIssueCatalog.BotMissingPermission(
                new BotRefusal(Guild, new ChannelId(20), "Send Messages", "send a message in <#20>"))
            .Summary.ShouldBe("Discord refused to let me send a message in <#20>: I don't have **Send Messages**.");

    [Fact]
    public void ForTheWholeGuild_ItNamesTheRoleOrder_WhenItMatters()
    {
        var issue = GuildIssueCatalog.BotMissingPermission(
            new BotRefusal(Guild, None, "Ban Members", "ban that member", AboutRoleOrder: true));

        issue.Code.ShouldBe("bot-missing-permission:Ban Members:guild");
        issue.Summary.ShouldBe("Discord refused to let me ban that member: I don't have **Ban Members**.");
        issue.SuggestedFix.ShouldBe("Give my role **Ban Members**. For an action on a member or a role, also move "
                                    + "my role above theirs in Server Settings > Roles.");
    }
}
