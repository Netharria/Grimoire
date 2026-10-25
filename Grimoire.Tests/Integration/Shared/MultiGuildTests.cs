// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.SharedDtos;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using Grimoire.Tests.Integration.FakeDiscord;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Tests.Integration.Shared;

/// <summary>
///     Multi-guild isolation (backlog A-35, and MOD-196). The bot is in two guilds, A and B, that share their users
///     but have their own channels, roles and settings. Nothing done in one should show or act in the other.
/// </summary>
[Collection("Test collection")]
public sealed class MultiGuildTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private static readonly TimeSpan NothingHappensWindow = TimeSpan.FromSeconds(2);

    private readonly GrimoireCoreFactory _database = database;
    private GrimoireTestBot _bot = null!;

    private FakeGuild A => this._bot.Guild;
    private FakeGuild B => this._bot.SecondGuild!;
    private SettingsModule Settings => this._bot.Services.GetRequiredService<SettingsModule>();
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await this._database.ResetDatabase();
        this._bot = await GrimoireTestBot.StartAsync(this._database, secondGuild: true);
        await this._bot.ConfigureModerationAsync();
        await this._bot.ConfigureModerationAsync(this.B);
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    [Fact]
    public async Task BothGuildsMembers_AreSyncedAtStartup()
    {
        await using var dbContext = this._database.CreateDbContext();
        (await dbContext.NicknameHistory.Where(row => row.UserId == new UserId(this.A.User1.Id)).Select(row => row.GuildId).ToListAsync(Token))
            .ShouldBe([new GuildId(this.A.Id), new GuildId(this.B.Id)], ignoreOrder: true);
    }

    /// <summary>A module turned on in A stays off in B.</summary>
    [Fact]
    public async Task ModuleSettings_AreSeparate()
    {
        await this._bot.SetModuleAsync(Module.Leveling, true);

        (await this._bot.RunSlashCommandAndWaitAsync(this.B.General, "level", this.B.User1)).Description
            .ShouldBe("This module is disabled in this server.");
        (await this._bot.RunSlashCommandAndWaitAsync(this.A.General, "level", this.A.User1)).Embed!["title"].ShouldNotBeNull();
    }

    /// <summary>MOD-196: banning in B bans only in B, records the sin in B, and logs to B's mod log.</summary>
    [Fact]
    public async Task ABanInB_OnlyAffectsB()
    {
        await this._bot.RunSlashCommandAndWaitAsync(this.B.General, "ban", this.B.Moderator,
            SlashOption.User("user", this.B.User1), SlashOption.String("reason", "spam"));

        await this._bot.Rest.WaitForRequestAsync(request => request.Method == HttpMethod.Put && request.Path == $"guilds/{this.B.Id}/bans/{this.B.User1.Id}");
        this._bot.Rest.Requests.ShouldNotContain(request => request.Path.StartsWith($"guilds/{this.A.Id}/bans", StringComparison.Ordinal));
        await using (var dbContext = this._database.CreateDbContext())
            (await dbContext.Sins.Select(sin => sin.GuildId).ToListAsync(Token)).ShouldBe([new GuildId(this.B.Id)]);
        await this._bot.Rest.WaitForRequestAsync(request => request.Method == HttpMethod.Post && request.Path == $"channels/{this.B.ModLog.Id}/messages");
        (await this._bot.Rest.ReceivesRequestWithinAsync(this._bot.IsModLogPost, NothingHappensWindow)).ShouldBeFalse("A's mod log stays quiet");
    }

    /// <summary>MOD-196: muting in B uses B's mute role and records the mute in B.</summary>
    [Fact]
    public async Task AMuteInB_OnlyAffectsB()
    {
        (await this.Settings.SetMuteRole(new GuildId(this.B.Id), new ModeratorId(this.B.Owner.Id), new RoleId(this.B.KickerRoleId))).ShouldSucceed();

        await this._bot.RunSlashCommandAndWaitAsync(this.B.General, "mute", this.B.Moderator, SlashOption.User("user", this.B.User1),
            SlashOption.Enum("duration_type", DurationType.Hours), SlashOption.Integer("duration_amount", 1));

        await this._bot.Rest.WaitForRequestAsync(request => request.Method == HttpMethod.Put
                                                           && request.Path == $"guilds/{this.B.Id}/members/{this.B.User1.Id}/roles/{this.B.KickerRoleId}");
        this.B.GetMember(this.B.User1).RoleIds.ShouldContain(this.B.KickerRoleId);
        this.A.GetMember(this.A.User1).RoleIds.ShouldBeEmpty();
        (await this.Settings.GetActiveMute(new UserId(this.B.User1.Id), new GuildId(this.B.Id), Token)).ShouldSucceed();
        (await this.Settings.GetActiveMute(new UserId(this.A.User1.Id), new GuildId(this.A.Id), Token))
            .ShouldNotBeOfType<Result<Grimoire.Settings.Domain.MuteAdded>.Success>();
    }

    /// <summary>Sins from A don't show in B's sin log.</summary>
    [Fact]
    public async Task SinsFromA_DoNotShowInB()
    {
        await this._bot.RunSlashCommandAndWaitAsync(this.A.General, "warn", this.A.Moderator,
            SlashOption.User("user", this.A.User1), SlashOption.String("reason", "rude in A"));

        var inB = await this._bot.RunSlashCommandAndWaitAsync(this.B.General, "sin_log", this.B.Moderator,
            SlashOption.Enum("type", Grimoire.Features.Moderation.SinAdmin.Commands.SinQueryType.All), SlashOption.User("user", this.B.User1));

        (inB.Description ?? string.Empty).ShouldNotContain("rude in A");
        var inA = await this._bot.RunSlashCommandAndWaitAsync(this.A.General, "sin_log", this.A.Moderator,
            SlashOption.Enum("type", Grimoire.Features.Moderation.SinAdmin.Commands.SinQueryType.All), SlashOption.User("user", this.A.User1));
        inA.Description.ShouldNotBeNull().ShouldContain("rude in A");
    }

    /// <summary>XP earned in A doesn't count in B.</summary>
    [Fact]
    public async Task XpFromA_DoesNotCountInB()
    {
        await this._bot.SetModuleAsync(Module.Leveling, true);
        await this._bot.SetModuleAsync(Module.Leveling, true, this.B);
        this._bot.SendMessage(this.A.User1, this.A.General, "hello");
        (await GrimoireTestBot.EventuallyAsync(async () =>
        {
            await using var dbContext = this._database.CreateDbContext();
            return await dbContext.XpHistory.CountAsync(Token);
        }, count => count == 1)).ShouldBe(1);

        var inB = (await this._bot.RunSlashCommandAndWaitAsync(this.B.General, "level", this.B.User1)).Embed!;

        inB["fields"]!.AsArray().Single(field => field!["name"]!.GetValue<string>() == "XP")!["value"]!.GetValue<string>().ShouldBe("0");
    }

    /// <summary>A custom command learned in A can't be triggered in B, and B can have its own of the same name.</summary>
    [Fact]
    public async Task CustomCommands_AreSeparate()
    {
        await this._bot.SetModuleAsync(Module.Commands, true);
        await this._bot.SetModuleAsync(Module.Commands, true, this.B);
        await this._bot.RunSlashCommandAndWaitAsync(this.A.General, "commands", this.A.Admin,
            SlashOption.Subcommand("learn", SlashOption.String("name", "hello"), SlashOption.String("content", "from A")));

        this._bot.SendMessage(this.B.User1, this.B.General, "!hello");
        (await this._bot.Rest.ReceivesRequestWithinAsync(request => request.Method == HttpMethod.Post
                                                                    && request.Path == $"channels/{this.B.General.Id}/messages", NothingHappensWindow))
            .ShouldBeFalse();

        await this._bot.RunSlashCommandAndWaitAsync(this.B.General, "commands", this.B.Admin,
            SlashOption.Subcommand("learn", SlashOption.String("name", "hello"), SlashOption.String("content", "from B")));
        this._bot.SendMessage(this.B.User1, this.B.General, "!hello");

        (await this._bot.Rest.WaitForRequestAsync(request => request.Method == HttpMethod.Post
                                                            && request.Path == $"channels/{this.B.General.Id}/messages"))
            .Json!["content"]!.GetValue<string>().ShouldBe("from B");
    }

    /// <summary>MOD-111: with a mute role only in A, a mute in B says B has none.</summary>
    [Fact]
    public async Task AMuteInB_DoesNotUseAsMuteRole()
    {
        (await this.Settings.SetMuteRole(new GuildId(this.A.Id), new ModeratorId(this.A.Owner.Id), new RoleId(this.A.KickerRoleId))).ShouldSucceed();

        (await this._bot.RunSlashCommandAndWaitAsync(this.B.General, "mute", this.B.Moderator, SlashOption.User("user", this.B.User1),
                SlashOption.Enum("duration_type", DurationType.Hours), SlashOption.Integer("duration_amount", 1)))
            .Description.ShouldBe("The mute role is not configured. Please configure it before using this command.");
    }

    /// <summary>MOD-89: B's /Mutes View shows B's own setting, not A's.</summary>
    [Fact]
    public async Task MutesViewInB_ShowsBsOwnSetting()
    {
        (await this.Settings.SetMuteRole(new GuildId(this.A.Id), new ModeratorId(this.A.Owner.Id), new RoleId(this.A.KickerRoleId))).ShouldSucceed();

        var reply = await this._bot.RunSlashCommandAndWaitAsync(this.B.General, "mutes", this.B.Admin, SlashOption.Subcommand("view"));

        reply.Embed!["fields"]!.AsArray().Single(field => field!["name"]!.GetValue<string>() == "Mute Role")!["value"]!
            .GetValue<string>().ShouldBe("None");
    }

    /// <summary>MOD-176: locking A's #general leaves B's #general alone.</summary>
    [Fact]
    public async Task ALockInA_LeavesBAlone()
    {
        await this._bot.RunSlashCommandAndWaitAsync(this.A.General, "lock", this.A.Moderator,
            SlashOption.Enum("duration_type", DurationType.Hours), SlashOption.Integer("duration_amount", 1));

        await this._bot.Rest.WaitForRequestAsync(request => request.Method == HttpMethod.Put
                                                           && request.Path.StartsWith($"channels/{this.A.General.Id}/permissions/", StringComparison.Ordinal));
        (await this._bot.Rest.ReceivesRequestWithinAsync(request => request.Path.StartsWith($"channels/{this.B.General.Id}/permissions/", StringComparison.Ordinal),
            NothingHappensWindow)).ShouldBeFalse();
        await using var settingsContext = this._database.CreateSettingsDbContext();
        (await settingsContext.ChannelLocks.Select(lockRow => lockRow.GuildId).ToListAsync(Token)).ShouldBe([new GuildId(this.A.Id)]);
    }
}
