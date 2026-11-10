// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text.Json.Nodes;
using Grimoire.Tests.Integration.FakeDiscord;

namespace Grimoire.Tests.Integration;

/// <summary>One choice the bot offered in an autocomplete answer: the label the member sees, and the value sent back.</summary>
public sealed record FakeChoice(string Name, string Value);

/// <summary>
///     Autocomplete (harness H-12). Discord sends <c>INTERACTION_CREATE</c> type 4 while a member types into an option
///     that has an autocomplete provider. The option being typed is marked <c>focused</c> and holds the partial text,
///     and the other options hold what is filled in so far. The bot answers with a type 8 callback listing its choices.
/// </summary>
public static class FakeAutocomplete
{
    private const int AutocompleteInteractionType = 4;
    private const int AutocompleteResultCallbackType = 8;

    private static int _interactionCount;

    /// <summary>
    ///     Sends the autocomplete request a member's client sends while typing. <paramref name="focused" /> names the
    ///     option being typed; it must be one of the leaf options in <paramref name="options" />, anywhere under a
    ///     subcommand, and its value is the partial text typed so far.
    /// </summary>
    public static FakeInteraction RunAutocomplete(this GrimoireTestBot bot, FakeChannel channel, string commandName,
        FakeUser invoker, string focused, params SlashOption[] options)
    {
        var guild = bot.GuildOf(channel);
        var number = Interlocked.Increment(ref _interactionCount);
        var interaction = new FakeInteraction(1_450_000_000_000_000_000UL + (ulong)number,
            $"fake-autocomplete-token-{number}");
        var memberJson = FakeGuild.MemberJson(guild.GetMember(invoker));
        memberJson["permissions"] = guild.GuildPermissionsOf(invoker).ToString(CultureInfo.InvariantCulture);

        var optionsJson = new JsonArray([.. options.Select(option => (JsonNode)option.ToJson())]);
        if (!MarkFocused(optionsJson, focused))
            throw new ArgumentException($"No option named {focused} to focus.", nameof(focused));

        bot.Gateway.Dispatch("INTERACTION_CREATE", new JsonObject
        {
            ["id"] = FakeUser.Snowflake(interaction.Id),
            ["application_id"] = FakeUser.Snowflake(guild.ApplicationId),
            ["type"] = AutocompleteInteractionType,
            ["token"] = interaction.Token,
            ["version"] = 1,
            ["guild_id"] = FakeUser.Snowflake(guild.Id),
            ["channel_id"] = FakeUser.Snowflake(channel.Id),
            ["channel"] = guild.ChannelJson(channel),
            ["member"] = memberJson,
            ["app_permissions"] = guild.BotPermissions.ToString(CultureInfo.InvariantCulture),
            ["locale"] = "en-US",
            ["guild_locale"] = "en-US",
            ["entitlements"] = new JsonArray(),
            ["authorizing_integration_owners"] = new JsonObject { ["0"] = FakeUser.Snowflake(guild.Id) },
            ["context"] = 0,
            ["data"] = new JsonObject
            {
                ["id"] = FakeUser.Snowflake(bot.Rest.GetCommandId(commandName)),
                ["name"] = commandName.ToLowerInvariant(),
                ["type"] = 1,
                ["guild_id"] = FakeUser.Snowflake(guild.Id),
                ["options"] = optionsJson,
                // Discord sends no resolved data with autocomplete, but DSharpPlus reads it without a null check.
                ["resolved"] = new JsonObject
                {
                    ["users"] = new JsonObject(),
                    ["members"] = new JsonObject(),
                    ["channels"] = new JsonObject(),
                    ["roles"] = new JsonObject()
                }
            }
        });

        return interaction;
    }

    /// <summary>Runs an autocomplete request from <c>#general</c> and returns the choices the bot offers.</summary>
    public static Task<IReadOnlyList<FakeChoice>> RunAutocompleteAndWaitAsync(this GrimoireTestBot bot,
        string commandName, FakeUser invoker, string focused, params SlashOption[] options)
        => bot.WaitForChoicesAsync(bot.RunAutocomplete(bot.Guild.General, commandName, invoker, focused, options));

    /// <summary>Runs an autocomplete request from <paramref name="channel" /> and returns the choices.</summary>
    public static Task<IReadOnlyList<FakeChoice>> RunAutocompleteAndWaitAsync(this GrimoireTestBot bot,
        FakeChannel channel, string commandName, FakeUser invoker, string focused, params SlashOption[] options)
        => bot.WaitForChoicesAsync(bot.RunAutocomplete(channel, commandName, invoker, focused, options));

    /// <summary>Waits for the type 8 callback answering <paramref name="interaction" /> and reads its choices.</summary>
    public static async Task<IReadOnlyList<FakeChoice>> WaitForChoicesAsync(this GrimoireTestBot bot,
        FakeInteraction interaction)
    {
        var callback = (await bot.Rest.WaitForRequestAsync(interaction.IsCallback)).Json!;
        callback["type"]!.GetValue<int>().ShouldBe(AutocompleteResultCallbackType);
        return
        [
            .. (callback["data"]?["choices"]?.AsArray() ?? []).Select(choice => new FakeChoice(
                choice!["name"]!.GetValue<string>(),
                choice["value"] is JsonValue value && value.TryGetValue<string>(out var text)
                    ? text
                    : choice["value"]!.ToJsonString()))
        ];
    }

    private static bool MarkFocused(JsonArray options, string focused)
    {
        foreach (var option in options.OfType<JsonObject>())
        {
            if (option["options"] is JsonArray nested)
            {
                if (MarkFocused(nested, focused))
                    return true;
            }
            else if (option["name"]?.GetValue<string>() == focused)
            {
                option["focused"] = true;
                return true;
            }
        }

        return false;
    }
}
