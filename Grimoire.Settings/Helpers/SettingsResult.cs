// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

namespace Grimoire.Settings.Helpers;

public abstract record SettingsResult
{
    public static SettingsResult Written() => new SettingsWritten();
    public static SettingsResult Unchanged() => new SettingsUnchanged();
    public static SettingsResult Invalid(string reason) => new SettingsInvalid(reason);
    public static SettingsResult<T> Written<T>(T value) => new SettingsWritten<T>(value);
    public static SettingsResult<T> Unchanged<T>(T value) => new SettingsUnchanged<T>(value);
    public static SettingsResult<T> Invalid<T>(string reason) => new SettingsInvalid<T>(reason);
}

public abstract record SettingsResult<T>() : SettingsResult;

public sealed record SettingsWritten : SettingsResult;

public sealed record SettingsWritten<T>(T InputValue) : SettingsResult<T>();

public sealed record SettingsUnchanged : SettingsResult;

public sealed record SettingsUnchanged<T>(T InputValue) : SettingsResult<T>();

public sealed record SettingsInvalid(string Reason) : SettingsResult;

public sealed record SettingsInvalid<T>(string Reason) : SettingsResult<T>();
