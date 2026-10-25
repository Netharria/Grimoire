// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text.Json.Nodes;

namespace Grimoire.Tests.Integration.FakeDiscord;

/// <summary>An emoji as it appears in gateway reaction events.</summary>
public abstract record FakeEmoji
{
    public abstract JsonObject ToJson();

    public sealed record Standard(string Unicode) : FakeEmoji
    {
        public override JsonObject ToJson() => new() { ["id"] = null, ["name"] = this.Unicode };
    }

    public sealed record Custom(ulong Id, string Name, bool IsAnimated = false) : FakeEmoji
    {
        public override JsonObject ToJson()
            => new() { ["id"] = FakeUser.Snowflake(this.Id), ["name"] = this.Name, ["animated"] = this.IsAnimated };
    }
}
