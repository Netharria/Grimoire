// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Net;
using System.Text.Json.Nodes;
using DSharpPlus.Extensions;
using Grimoire.Features.Shared.Alerts;
using Grimoire.IntegrationTests.FakeDiscord;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Serilog.Events;

namespace Grimoire.IntegrationTests.Shared.Boundary;

/// <summary>
///     The 1.2 boundaries inside the real host (spec 1.2, section 8), through the test-only probes: what Discord is
///     sent for each outcome, what is alerted, and that nothing reaches DSharpPlus's error events.
/// </summary>
[Collection("Test collection")]
public sealed class BoundaryTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private static readonly TimeSpan NothingHappensWindow = TimeSpan.FromSeconds(2);

    private readonly RecordingAlerts _alerts = new();
    private readonly BoundaryProbeWorkerControl _worker = new();
    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;

    public async ValueTask InitializeAsync()
    {
        await database.ResetDatabase();
        this._bot = await GrimoireTestBot.StartAsync(database,
            unregisteredCommands: [typeof(BoundaryProbeCommands)],
            configureHost: builder =>
            {
                builder.Services.Replace(ServiceDescriptor.Singleton<IAlertSender>(this._alerts));
                builder.Services.AddSingleton(this._worker);
                builder.Services.ConfigureEventHandlers(handlers =>
                    handlers.AddEventHandlers<BoundaryProbeEventHandler>());
            });
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    // ── Slash ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task APublicCommand_DefersPublicly_AndEditsTheResponse()
    {
        var reply = await this._bot.RunSlashCommandAndWaitAsync("probe_public", this.Guild.User1,
            SlashOption.String("mode", "ok"));

        reply.WasDeferred.ShouldBeTrue();
        reply.IsEphemeral.ShouldBeFalse();
        reply.Description.ShouldBe(BoundaryProbeCommands.Ok);
    }

    [Fact]
    public async Task APrivateCommand_DefersEphemerally()
    {
        var reply = await this._bot.RunSlashCommandAndWaitAsync("probe_private", this.Guild.User1,
            SlashOption.String("mode", "ok"));

        reply.WasDeferred.ShouldBeTrue();
        reply.IsEphemeral.ShouldBeTrue();
        reply.Description.ShouldBe(BoundaryProbeCommands.Ok);
    }

    [Fact]
    public async Task AFailedCheck_IsAnEphemeralResponse_WithNoDefer()
    {
        var reply = await this._bot.RunSlashCommandAndWaitAsync("probe_public", this.Guild.User1,
            SlashOption.String("mode", "invalid"));

        reply.WasDeferred.ShouldBeFalse();
        reply.IsEphemeral.ShouldBeTrue();
        reply.Description.ShouldBe("probe refused");
    }

    [Fact]
    public async Task ARefusalAfterTheDefer_DeletesTheResponse_AndFollowsUpEphemerally()
    {
        var interaction = this._bot.RunSlashCommand("probe_public", this.Guild.User1,
            SlashOption.String("mode", "notfound"));

        var reply = await FakeReply.WaitForAsync(this._bot.Rest, interaction);

        reply.WasDeferred.ShouldBeTrue();
        reply.IsEphemeral.ShouldBeTrue();
        reply.Description.ShouldBe("probe not found");
        this._bot.Rest.Requests.Any(interaction.IsOriginalResponseDelete).ShouldBeTrue();
    }

    [Theory]
    [InlineData("defect", "UnhandledException")]
    [InlineData("unavailable", "Unavailable")]
    public async Task AnOutageOrADefect_GetsTheGenericReply_AndOneUrgentAlertWithItsId(string mode, string type)
    {
        var reply = await this._bot.RunSlashCommandAndWaitAsync("probe_public", this.Guild.User1,
            SlashOption.String("mode", mode));

        reply.IsEphemeral.ShouldBeTrue();
        reply.Description.ShouldStartWith("Something went wrong while running `/probe_public`.");
        var errorId = reply.Description!.Split('`')[^2];
        // Only the urgent one: CommandHandler may also raise a SlowCommand warning once the command finishes, since
        // the fake interaction's id dates it long ago. Whether that has arrived yet is a race.
        var alert = this._alerts.Sent.Where(sent => sent.Severity == AlertSeverity.Urgent).ShouldHaveSingleItem();
        alert.Type.ShouldBe(type);
        alert.Severity.ShouldBe(AlertSeverity.Urgent);
        alert.Message.ShouldContain(errorId);
        alert.GuildId.ShouldBe(new GuildId(this.Guild.Id));
    }

    [Fact]
    public async Task ADefect_NeverReachesDSharpPlussErrorEvent()
    {
        await this._bot.RunSlashCommandAndWaitAsync("probe_public", this.Guild.User1,
            SlashOption.String("mode", "defect"));

        // CommandHandler's CommandErrored path would log "Error on Command" under its own source, with the
        // exception DSharpPlus caught. The boundary logs it once, under Grimoire.Commands.
        var finished = await this._bot.Logs.WaitForAsync(IsFinishedLine, TimeSpan.FromSeconds(10));
        finished.Properties["Outcome"].ToString().ShouldBe("Defect");
        this._bot.Logs.Events.Count(logEvent => logEvent.MessageTemplate.Text.StartsWith("Error on Command"))
            .ShouldBe(1);
    }

    [Fact]
    public async Task TheFinishedLogLine_CarriesTheOutcome()
    {
        await this._bot.RunSlashCommandAndWaitAsync("probe_public", this.Guild.User1,
            SlashOption.String("mode", "invalid"));

        var finished = await this._bot.Logs.WaitForAsync(IsFinishedLine, TimeSpan.FromSeconds(10));
        finished.Properties["Outcome"].ToString().ShouldBe("Invalid");
    }

    // ── Text ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task APublicTextCommand_RepliesPublicly()
    {
        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "!probe_public ok");

        var post = await this._bot.Rest.WaitForRequestAsync(this.IsGeneralPost);
        DescriptionOf(post).ShouldBe(BoundaryProbeCommands.Ok);
    }

    [Fact]
    public async Task APublicTextCommand_IsRefusedPublicly()
    {
        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "!probe_public invalid");

        var post = await this._bot.Rest.WaitForRequestAsync(this.IsGeneralPost);
        DescriptionOf(post).ShouldBe("probe refused");
    }

    [Fact]
    public async Task APrivateTextCommand_ReactsWithALock_AndSendsNothing()
    {
        var messageId = this._bot.SendMessage(this.Guild.User1, this.Guild.General, "!probe_private ok");

        await this._bot.Rest.WaitForRequestAsync(request => request.Method == HttpMethod.Put
            && request.Path == $"channels/{this.Guild.General.Id}/messages/{messageId}/reactions/🔒/@me");
        (await this._bot.Rest.ReceivesRequestWithinAsync(this.IsGeneralPost, NothingHappensWindow)).ShouldBeFalse();
    }

    [Fact]
    public async Task ADirectMessageTextCommand_RepliesInTheDms_NotTheChannel()
    {
        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "!probe_dm ok");

        var post = await this._bot.Rest.WaitForRequestAsync(this.IsDirectMessageTo(this.Guild.User1));
        DescriptionOf(post).ShouldBe(BoundaryProbeCommands.Ok);
        (await this._bot.Rest.ReceivesRequestWithinAsync(this.IsGeneralPost, NothingHappensWindow)).ShouldBeFalse();
    }

    [Fact]
    public async Task ADirectMessageTextCommand_WithClosedDms_ReactsWithALockInstead()
    {
        this._bot.Rest.On(HttpMethod.Post, $"channels/{this.Guild.User1.Id + 1000}/messages",
            _ => FakeDiscordRest.Error(HttpStatusCode.Forbidden, 50007, "Cannot send messages to this user"));

        var messageId = this._bot.SendMessage(this.Guild.User1, this.Guild.General, "!probe_dm ok");

        await this._bot.Rest.WaitForRequestAsync(request => request.Method == HttpMethod.Put
            && request.Path == $"channels/{this.Guild.General.Id}/messages/{messageId}/reactions/🔒/@me");
        this._alerts.Sent.ShouldBeEmpty();
    }

    // ── Events and workers ────────────────────────────────────────────────────

    [Fact]
    public async Task AnEventHandlerDefect_IsOneUrgentAlert_AndNotDSharpPlussErrorHandler()
    {
        this._bot.SendMessage(this.Guild.User1, this.Guild.General, BoundaryProbeEventHandler.Trigger);

        var alerts = await GrimoireTestBot.EventuallyAsync(() => Task.FromResult(this._alerts.Sent),
            sent => sent.Any(alert => alert.Discriminator == nameof(BoundaryProbeEventHandler)));
        var alert = alerts.ShouldHaveSingleItem();
        alert.Type.ShouldBe("UnhandledException");
        alert.Severity.ShouldBe(AlertSeverity.Urgent);
        this._bot.Logs.Events.ShouldNotContain(logEvent =>
            logEvent.MessageTemplate.Text.StartsWith("Unhandled exception in event handler"));
    }

    [Fact]
    public async Task AFailedWorkerItem_IsRetriedOnlyAfterItsBackoff()
    {
        await this._bot.RunEffectTickAsync<BoundaryProbeWorker>();
        await this._bot.RunEffectTickAsync<BoundaryProbeWorker>();

        this._worker.Attempts.ShouldBe(1);
        var alert = this._alerts.Sent.ShouldHaveSingleItem();
        alert.Type.ShouldBe("BackgroundTaskItemFailure");
        alert.Severity.ShouldBe(AlertSeverity.Warning);
    }

    private static bool IsFinishedLine(LogEvent logEvent)
        => logEvent.MessageTemplate.Text.StartsWith("Command {Command} finished with {Outcome}")
           && logEvent.Properties["Command"].ToString().Contains("probe_public");

    private bool IsGeneralPost(RecordedRequest request)
        => request.Method == HttpMethod.Post && request.Path == $"channels/{this.Guild.General.Id}/messages";

    private Func<RecordedRequest, bool> IsDirectMessageTo(FakeUser user)
        => request => request.Method == HttpMethod.Post && request.Path == $"channels/{user.Id + 1000}/messages";

    private static string? DescriptionOf(RecordedRequest request)
        => request.Json?["embeds"]?.AsArray().FirstOrDefault()?["description"]?.GetValue<string>();
}
