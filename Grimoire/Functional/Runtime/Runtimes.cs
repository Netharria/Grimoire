// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Functional.Capabilities;
using LanguageExt;
using LanguageExt.Traits;

namespace Grimoire.Functional.Runtime;

/// <summary>
///     The capabilities every runtime shares. Production builds it once from DI; a unit test builds one from fakes.
///     Sections 1.3 (database), 1.4 (Discord, guild issues), 1.5 (PluralKit) and 3.1 (settings) each add a field here and a <c>Has</c> per
///     runtime.
/// </summary>
public sealed record GrimoireEnv(
    ITimeIO Time,
    ILogIO Log,
    IAlertIO Alerts,
    IGuildLogIO GuildLog,
    ITraceIO Trace,
    IDatabaseIO Database,
    IDiscordIO Discord,
    IGuildIssueIO GuildIssues,
    IPluralKitIO PluralKit);

/// <summary>
///     The runtime for event handlers and background workers. Each <c>Has</c> tells language-ext where to find a
///     capability, which is what lets <c>Clock&lt;RT&gt;</c> and the other modules work with any runtime that has it.
/// </summary>
public sealed record GrimoireRuntime(GrimoireEnv Env) :
    Has<Eff<GrimoireRuntime>, ITimeIO>,
    Has<Eff<GrimoireRuntime>, ILogIO>,
    Has<Eff<GrimoireRuntime>, IAlertIO>,
    Has<Eff<GrimoireRuntime>, IGuildLogIO>,
    Has<Eff<GrimoireRuntime>, ITraceIO>,
    Has<Eff<GrimoireRuntime>, IDatabaseIO>,
    Has<Eff<GrimoireRuntime>, IDiscordIO>,
    Has<Eff<GrimoireRuntime>, IGuildIssueIO>,
    Has<Eff<GrimoireRuntime>, IPluralKitIO>
{
    static K<Eff<GrimoireRuntime>, ITimeIO> Has<Eff<GrimoireRuntime>, ITimeIO>.Ask => Get(rt => rt.Env.Time);
    static K<Eff<GrimoireRuntime>, ILogIO> Has<Eff<GrimoireRuntime>, ILogIO>.Ask => Get(rt => rt.Env.Log);
    static K<Eff<GrimoireRuntime>, IAlertIO> Has<Eff<GrimoireRuntime>, IAlertIO>.Ask => Get(rt => rt.Env.Alerts);

    static K<Eff<GrimoireRuntime>, IGuildLogIO> Has<Eff<GrimoireRuntime>, IGuildLogIO>.Ask =>
        Get(rt => rt.Env.GuildLog);

    static K<Eff<GrimoireRuntime>, ITraceIO> Has<Eff<GrimoireRuntime>, ITraceIO>.Ask => Get(rt => rt.Env.Trace);

    static K<Eff<GrimoireRuntime>, IDatabaseIO> Has<Eff<GrimoireRuntime>, IDatabaseIO>.Ask =>
        Get(rt => rt.Env.Database);

    static K<Eff<GrimoireRuntime>, IDiscordIO> Has<Eff<GrimoireRuntime>, IDiscordIO>.Ask => Get(rt => rt.Env.Discord);

    static K<Eff<GrimoireRuntime>, IGuildIssueIO> Has<Eff<GrimoireRuntime>, IGuildIssueIO>.Ask =>
        Get(rt => rt.Env.GuildIssues);

    static K<Eff<GrimoireRuntime>, IPluralKitIO> Has<Eff<GrimoireRuntime>, IPluralKitIO>.Ask =>
        Get(rt => rt.Env.PluralKit);

    private static Eff<GrimoireRuntime, A> Get<A>(Func<GrimoireRuntime, A> select) =>
        Eff<GrimoireRuntime, A>.Lift(select);
}

/// <summary>
///     The runtime for one command invocation: everything in <see cref="GrimoireRuntime" /> plus the reply capability.
///     It's built per invocation, because the reply belongs to that interaction.
/// </summary>
public sealed record CommandRuntime(GrimoireEnv Env, ICommandIO Command) :
    Has<Eff<CommandRuntime>, ITimeIO>,
    Has<Eff<CommandRuntime>, ILogIO>,
    Has<Eff<CommandRuntime>, IAlertIO>,
    Has<Eff<CommandRuntime>, IGuildLogIO>,
    Has<Eff<CommandRuntime>, ITraceIO>,
    Has<Eff<CommandRuntime>, IDatabaseIO>,
    Has<Eff<CommandRuntime>, IDiscordIO>,
    Has<Eff<CommandRuntime>, IGuildIssueIO>,
    Has<Eff<CommandRuntime>, IPluralKitIO>,
    Has<Eff<CommandRuntime>, ICommandIO>
{
    static K<Eff<CommandRuntime>, ITimeIO> Has<Eff<CommandRuntime>, ITimeIO>.Ask => Get(rt => rt.Env.Time);
    static K<Eff<CommandRuntime>, ILogIO> Has<Eff<CommandRuntime>, ILogIO>.Ask => Get(rt => rt.Env.Log);
    static K<Eff<CommandRuntime>, IAlertIO> Has<Eff<CommandRuntime>, IAlertIO>.Ask => Get(rt => rt.Env.Alerts);
    static K<Eff<CommandRuntime>, IGuildLogIO> Has<Eff<CommandRuntime>, IGuildLogIO>.Ask => Get(rt => rt.Env.GuildLog);
    static K<Eff<CommandRuntime>, ITraceIO> Has<Eff<CommandRuntime>, ITraceIO>.Ask => Get(rt => rt.Env.Trace);
    static K<Eff<CommandRuntime>, IDatabaseIO> Has<Eff<CommandRuntime>, IDatabaseIO>.Ask => Get(rt => rt.Env.Database);
    static K<Eff<CommandRuntime>, IDiscordIO> Has<Eff<CommandRuntime>, IDiscordIO>.Ask => Get(rt => rt.Env.Discord);

    static K<Eff<CommandRuntime>, IGuildIssueIO> Has<Eff<CommandRuntime>, IGuildIssueIO>.Ask =>
        Get(rt => rt.Env.GuildIssues);

    static K<Eff<CommandRuntime>, IPluralKitIO> Has<Eff<CommandRuntime>, IPluralKitIO>.Ask =>
        Get(rt => rt.Env.PluralKit);
    static K<Eff<CommandRuntime>, ICommandIO> Has<Eff<CommandRuntime>, ICommandIO>.Ask => Get(rt => rt.Command);

    private static Eff<CommandRuntime, A> Get<A>(Func<CommandRuntime, A> select) => Eff<CommandRuntime, A>.Lift(select);
}
