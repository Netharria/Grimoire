// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Dapper;
using LanguageExt;

namespace Grimoire.Functional.Data;

/// <summary>
///     A statement and its parameters, as a value. Built by pure functions in a feature's SQL module; only
///     <c>IDatabaseIO</c> runs it (spec 1.3, section 3.1).
/// </summary>
/// <remarks>
///     Whether it only reads is part of the value because the method can't say: an <c>INSERT … RETURNING</c> goes
///     through <c>Query</c>, and retrying it would insert twice. Only reads are retried.
/// </remarks>
public sealed record Sql
{
    private Sql(string name, string text, object? parameters, bool isRead)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A statement needs a name, such as \"Warn.InsertSin\".", nameof(name));
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException($"Statement {name} has no text.", nameof(text));

        this.Name = name;
        this.Text = text;
        this.Parameters = parameters;
        this.IsRead = isRead;
    }

    /// <summary>
    ///     "Feature.Statement". The fake database answers by it, error logs name it, and 1.6 makes it the span name.
    ///     Never the SQL text, which may carry values.
    /// </summary>
    public string Name { get; }

    public string Text { get; }

    /// <summary>An anonymous object or <see cref="DynamicParameters" />, as Dapper takes them.</summary>
    public object? Parameters { get; }

    /// <summary>True only for a statement that changes nothing. Only these are retried.</summary>
    public bool IsRead { get; }

    /// <summary>The parameter names, never their values: what an error log may say about the parameters.</summary>
    public Seq<string> ParameterNames => this.Parameters switch
    {
        null => [],
        DynamicParameters dynamic => toSeq(dynamic.ParameterNames),
        var parameters => toSeq(parameters.GetType().GetProperties().Select(property => property.Name))
    };

    public static Sql Read(string name, string text, object? parameters = null) => new(name, text, parameters, true);

    public static Sql Write(string name, string text, object? parameters = null) => new(name, text, parameters, false);
}
