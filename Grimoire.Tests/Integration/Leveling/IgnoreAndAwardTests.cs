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
using XpOption = Grimoire.Features.Leveling.Awards.ReclaimUserXp.XpOption;

namespace Grimoire.Tests.Integration.Leveling;

/// <summary>
///     The leveling <c>/Ignore</c> Add, Remove and View, and <c>/Award</c> and <c>/Reclaim</c> (backlog A-28). Ignore's
///     options are mentionable, named <c>items_N</c> for Add and <c>item_N</c> for Remove.
/// </summary>
[Collection("Test collection")]
public sealed class IgnoreAndAwardTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private static readonly TimeSpan NothingHappensWindow = TimeSpan.FromSeconds(2);

    private readonly GrimoireCoreFactory _database = database;
    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;
    private SettingsModule Settings => this._bot.Services.GetRequiredService<SettingsModule>();
    private GuildId GuildId => new(this.Guild.Id);
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await this._database.ResetDatabase();
        this._bot = await GrimoireTestBot.StartAsync(this._database);
        await this._bot.ConfigureModerationAsync();
        await this._bot.SetModuleAsync(Module.Leveling, true);
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    // ── /Ignore ───────────────────────────────────────────────────────────────

    /// <summary>Ignoring a member stops their XP, and it's stored and logged.</summary>
    /// <remarks>Cases: LVL-120.</remarks>
    [Fact]
    public async Task IgnoreAdd_StopsXp_AndIsLogged()
    {
        var expected = $"<@{this.Guild.User1.Id}> <@&{this.Guild.KickerRoleId}> are now ignored for xp gain.";

        (await this.IgnoreAsync("add", SlashOption.MentionableUser("items_0", this.Guild.User1),
            SlashOption.MentionableRole("items_1", this.Guild.KickerRoleId))).Description.ShouldBe(expected);

        await this._bot.WaitForModLogEmbedAsync(e => e["description"]?.GetValue<string>() == expected);
        var ignored = (await this.Settings.GetAllIgnoredItems(this.GuildId, cancellationToken: TestContext.Current.CancellationToken)).ShouldSucceed();
        ignored.OfType<IgnoredMember>().ShouldHaveSingleItem().UserId.ShouldBe(new UserId(this.Guild.User1.Id));
        ignored.OfType<IgnoredRole>().ShouldHaveSingleItem().RoleId.ShouldBe(new RoleId(this.Guild.KickerRoleId));
        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "hello");
        (await this.XpOfAsync(this.Guild.User1, expectAtLeast: 1, NothingHappensWindow)).ShouldBe(0);
    }

    /// <remarks>Cases: LVL-124.</remarks>
    [Fact]
    public async Task IgnoreRemove_LetsXpThroughAgain()
    {
        await this.IgnoreAsync("add", SlashOption.MentionableUser("items_0", this.Guild.User1));

        await this.IgnoreAsync("remove", SlashOption.MentionableUser("items_0", this.Guild.User1));

        (await this.Settings.GetAllIgnoredItems(this.GuildId, cancellationToken: TestContext.Current.CancellationToken)).ShouldSucceed().ShouldBeEmpty();
        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "hello");
        (await this.XpOfAsync(this.Guild.User1, expectAtLeast: 1)).ShouldBeGreaterThan(0);
    }

    /// <remarks>Cases: LVL-124.</remarks>
    [Fact]
    public async Task IgnoreRemove_SaysWhatIsNoLongerIgnored()
    {
        await this.IgnoreAsync("add", SlashOption.MentionableUser("items_0", this.Guild.User1));

        (await this.IgnoreAsync("remove", SlashOption.MentionableUser("items_0", this.Guild.User1))).Description
            .ShouldBe($"<@{this.Guild.User1.Id}> are no longer ignored for xp gain.");
    }

    /// <summary>Review X-12: adding what's already ignored changes nothing, says so, and isn't logged.</summary>
    [Fact]
    public async Task IgnoreAdd_OfAnIgnoredItem_SaysSo_AndSavesNothing()
    {
        await this.IgnoreAsync("add", SlashOption.MentionableUser("items_0", this.Guild.User1));

        (await this.IgnoreAsync("add", SlashOption.MentionableUser("items_0", this.Guild.User1))).Description
            .ShouldBe($"<@{this.Guild.User1.Id}> were already ignored.");

        await using var settingsContext = this._database.CreateSettingsDbContext();
        (await settingsContext.XpTrackedItems.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(1);
        await Task.Delay(NothingHappensWindow, TestContext.Current.CancellationToken);
        this._bot.Rest.Requests.Count(request => this._bot.IsModLogPost(request)
                                                 && request.Body?.Contains("ignored for xp gain", StringComparison.Ordinal) == true)
            .ShouldBe(1);
    }

    /// <summary>Review X-12: with a mix, only the real changes are saved and logged, and the rest are named.</summary>
    [Fact]
    public async Task IgnoreAdd_WithSomeAlreadyIgnored_ReportsBoth()
    {
        await this.IgnoreAsync("add", SlashOption.MentionableUser("items_0", this.Guild.User1));

        (await this.IgnoreAsync("add", SlashOption.MentionableUser("items_0", this.Guild.User1),
                SlashOption.MentionableRole("items_1", this.Guild.KickerRoleId))).Description
            .ShouldBe($"<@&{this.Guild.KickerRoleId}> are now ignored for xp gain.\n<@{this.Guild.User1.Id}> were already ignored.");

        await this._bot.WaitForModLogEmbedAsync(e => e["description"]?.GetValue<string>()
            == $"<@&{this.Guild.KickerRoleId}> are now ignored for xp gain.");
        await using var settingsContext = this._database.CreateSettingsDbContext();
        (await settingsContext.XpTrackedItems.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(2);
    }

    /// <summary>Review X-12: removing what isn't ignored changes nothing and says so.</summary>
    [Fact]
    public async Task IgnoreRemove_OfAnItemNotIgnored_SaysSo_AndSavesNothing()
    {
        (await this.IgnoreAsync("remove", SlashOption.MentionableUser("items_0", this.Guild.User1))).Description
            .ShouldBe($"<@{this.Guild.User1.Id}> were not ignored.");

        await using var settingsContext = this._database.CreateSettingsDbContext();
        (await settingsContext.XpTrackedItems.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(0);
    }

    /// <remarks>Cases: LVL-128.</remarks>
    [Fact]
    public async Task IgnoreView_WithNothingIgnored_SaysSo()
        => (await this.IgnoreAsync("view")).Description.ShouldBe("This server does not have any ignored channels, roles or users.");

    /// <remarks>Cases: LVL-127.</remarks>
    [Fact]
    public async Task IgnoreView_ListsChannelsRolesAndUsers()
    {
        (await this.Settings.AppendIgnoredItemsEvent(this.GuildId, new HashSet<XpTrackedItem>
        {
            IgnoredChannel.Create(new ChannelId(this.Guild.General.Id), this.GuildId, new ModeratorId(this.Guild.Owner.Id), DateTimeOffset.UtcNow).ShouldSucceed(),
            IgnoredRole.Create(new RoleId(this.Guild.KickerRoleId), this.GuildId, new ModeratorId(this.Guild.Owner.Id), DateTimeOffset.UtcNow).ShouldSucceed(),
            IgnoredMember.Create(new UserId(this.Guild.User1.Id), this.GuildId, new ModeratorId(this.Guild.Owner.Id), DateTimeOffset.UtcNow).ShouldSucceed()
        }, cancellationToken: TestContext.Current.CancellationToken)).ShouldSucceed();

        var reply = await this.IgnoreAsync("view");

        reply.Embed!["title"]!.GetValue<string>().ShouldBe("Ignored Channels Roles and Users.");
        reply.Description.ShouldNotBeNull().ShouldContain($"**Channels**\n{this.Guild.General.Mention}");
        reply.Description.ShouldContain($"**Roles**\n<@&{this.Guild.KickerRoleId}>");
        reply.Description.ShouldContain($"**Users**\n<@{this.Guild.User1.Id}>");
    }

    /// <summary>X-3: one item per line, so the paginator can split a long list across pages.</summary>
    /// <remarks>Cases: LVL-130.</remarks>
    [Fact]
    public async Task IgnoreView_WithHundredsOfItems_IsSplitAcrossPages()
    {
        (await this.Settings.AppendIgnoredItemsEvent(this.GuildId, Enumerable.Range(0, 300)
            .Select(i => (XpTrackedItem)IgnoredMember.Create(new UserId(1_100_000_000_000_100_000 + (ulong)i), this.GuildId,
                new ModeratorId(this.Guild.Owner.Id), DateTimeOffset.UtcNow).ShouldSucceed())
            .ToHashSet(), cancellationToken: TestContext.Current.CancellationToken)).ShouldSucceed();

        var reply = await this.IgnoreAsync("view");

        reply.Description.ShouldNotBeNull().Length.ShouldBeLessThanOrEqualTo(4096);
        reply.Description.ShouldContain("**Users**\n<@1100000000000100");
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>Discord's mentionable options only offer users and roles, so channels get their own optional option.</summary>
    /// <remarks>Cases: LVL-121.</remarks>
    [Theory]
    [InlineData("add")]
    [InlineData("remove")]
    public void Ignore_OffersAChannelOption(string subcommand)
    {
        var options = this._bot.Rest.RegisteredCommands["ignore"]["options"]!.AsArray()
            .Single(option => option!["name"]!.GetValue<string>() == subcommand)!["options"]!.AsArray();

        var channel = options.Single(option => option!["name"]!.GetValue<string>() == "channel")!;
        channel["type"]!.GetValue<int>().ShouldBe(7);
        options.ShouldAllBe(option => option!["required"] == null || !option["required"]!.GetValue<bool>());
    }

    /// <remarks>Cases: LVL-121, LVL-125.</remarks>
    [Fact]
    public async Task Ignore_AChannel_CanBeAddedAndRemoved()
    {
        (await this.IgnoreAsync("add", SlashOption.Channel("channel", this.Guild.General))).Description
            .ShouldBe($"{this.Guild.General.Mention} are now ignored for xp gain.");
        (await this.Settings.GetAllIgnoredItems(this.GuildId, cancellationToken: TestContext.Current.CancellationToken)).ShouldSucceed().ShouldHaveSingleItem()
            .ShouldBeOfType<IgnoredChannel>();

        (await this.IgnoreAsync("remove", SlashOption.Channel("channel", this.Guild.General))).Description
            .ShouldBe($"{this.Guild.General.Mention} are no longer ignored for xp gain.");
        (await this.Settings.GetAllIgnoredItems(this.GuildId, cancellationToken: TestContext.Current.CancellationToken)).ShouldSucceed().ShouldBeEmpty();
    }

    /// <remarks>Cases: LVL-122.</remarks>
    [Fact]
    public async Task IgnoreAdd_WithNothingSelected_SaysSo()
        => (await this.IgnoreAsync("add")).Description.ShouldBe("Could not parse any ids from the submitted values.");

    /// <remarks>Cases: LVL-132 (Add only).</remarks>
    [Fact]
    public async Task IgnoreAdd_ByAMemberWithoutManageGuild_IsRefused()
        => (await this._bot.RunSlashCommandAndWaitAsync("ignore", this.Guild.Moderator,
                SlashOption.Subcommand("add", SlashOption.MentionableUser("items_0", this.Guild.User1))))
            .Description.ShouldBe("You do not have the required permissions to use this command.");

    // ── /Award and /Reclaim ───────────────────────────────────────────────────

    /// <remarks>Cases: LVL-140.</remarks>
    [Fact]
    public async Task Award_AddsXp_AndIsLogged()
    {
        var reply = await this._bot.RunSlashCommandAndWaitAsync("award", this.Guild.Moderator,
            SlashOption.User("user", this.Guild.User1), SlashOption.Integer("xp", 50));

        reply.Description.ShouldBe($"{this.Guild.User1.MemberMention} has been awarded 50 xp.");
        (await this.XpOfAsync(this.Guild.User1)).ShouldBe(50);
        await using (var dbContext = this._database.CreateDbContext())
            (await dbContext.XpHistory.OfType<AwardedXp>().SingleAsync(Token)).AwarderId.ShouldBe(new ModeratorId(this.Guild.Moderator.Id));
        await this._bot.WaitForModLogEmbedAsync(e => e["description"]?.GetValue<string>()
            == $"{this.Guild.User1.MemberMention} has been awarded 50 xp by {this.Guild.Moderator.MemberMention}.");
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <remarks>Cases: LVL-147 (Amount, 20), LVL-148 (Amount, 80), LVL-149 (All).</remarks>
    [Theory]
    [InlineData(XpOption.Amount, 20, 20, 30)]
    [InlineData(XpOption.Amount, 80, 50, 0)]
    [InlineData(XpOption.All, 0, 50, 0)]
    public async Task Reclaim_TakesXp_NeverMoreThanTheyHave(XpOption option, int amount, int taken, int left)
    {
        await this.SeedXpAsync(this.Guild.User1, 50);

        var reply = await this.ReclaimAsync(option, amount);

        reply.Description.ShouldBe($"{taken} xp has been taken from {this.Guild.User1.Mention}.");
        (await this.XpOfAsync(this.Guild.User1)).ShouldBe(left);
        // Review P-2: who took it is kept.
        await using (var dbContext = this._database.CreateDbContext())
            (await dbContext.XpHistory.OfType<ReclaimedXp>().SingleAsync(Token)).ModeratorId
                .ShouldBe(new ModeratorId(this.Guild.Moderator.Id));
        await this._bot.WaitForModLogEmbedAsync(e => e["description"]?.GetValue<string>()
            == $"{taken} xp has been taken from {this.Guild.User1.Mention} by {this.Guild.Moderator.MemberMention}.");
    }

    /// <remarks>Cases: LVL-152.</remarks>
    [Fact]
    public async Task Reclaim_FromAMemberWithNoXp_SaysSo()
        => (await this.ReclaimAsync(XpOption.All, 0)).Description.ShouldBe($"{this.Guild.User1.Mention} has no xp to take.");

    /// <remarks>Cases: LVL-151.</remarks>
    [Fact]
    public async Task Reclaim_AnAmountOfZero_IsRejected()
    {
        await this.SeedXpAsync(this.Guild.User1, 50);

        (await this.ReclaimAsync(XpOption.Amount, 0)).Description.ShouldBe("Specify an amount greater than 0");
        (await this.XpOfAsync(this.Guild.User1)).ShouldBe(50);
    }

    /// <remarks>Cases: LVL-155.</remarks>
    [Theory]
    [InlineData("award")]
    [InlineData("reclaim")]
    public async Task AwardAndReclaim_NeedManageMessages(string command)
        => (await this._bot.RunSlashCommandAndWaitAsync(command, this.Guild.User1, command == "award"
                ? [SlashOption.User("user", this.Guild.Moderator), SlashOption.Integer("xp", 5)]
                : [SlashOption.User("user", this.Guild.Moderator), SlashOption.Enum("option", XpOption.All)]))
            .Description.ShouldBe("You do not have the required permissions to use this command.");

    private Task<FakeReply> IgnoreAsync(string subcommand, params SlashOption[] options)
        => this._bot.RunSlashCommandAndWaitAsync("ignore", this.Guild.Admin, SlashOption.Subcommand(subcommand, options));

    private Task<FakeReply> ReclaimAsync(XpOption option, int amount)
        => this._bot.RunSlashCommandAndWaitAsync("reclaim", this.Guild.Moderator,
        [
            SlashOption.User("user", this.Guild.User1), SlashOption.Enum("option", option),
            .. amount == 0 ? Array.Empty<SlashOption>() : [SlashOption.Integer("amount", amount)]
        ]);

    private async Task SeedXpAsync(FakeUser user, int xp)
    {
        await using var seed = this._database.CreateDbContext();
        seed.XpHistory.Add(EarnedXp.Create(PositiveXpAmount.Create(xp).ShouldSucceed(), new UserId(user.Id), this.GuildId,
            DateTimeOffset.UtcNow.AddMinutes(-10)));
        await seed.SaveChangesAsync(Token);
    }

    private async Task<long> XpOfAsync(FakeUser user, int expectAtLeast = 0, TimeSpan? window = null)
    {
        var entries = await GrimoireTestBot.EventuallyAsync(async () =>
        {
            await using var dbContext = this._database.CreateDbContext();
            return await dbContext.XpHistory.Where(xp => xp.UserId == new UserId(user.Id)).ToListAsync(Token);
        }, found => found.Count >= expectAtLeast, window);
        return entries.Sum(entry => entry switch
        {
            EarnedXp earned => earned.Xp.Value,
            AwardedXp awarded => awarded.Xp.Value,
            ReclaimedXp reclaimed => reclaimed.Xp.Value,
            _ => 0
        });
    }
}
