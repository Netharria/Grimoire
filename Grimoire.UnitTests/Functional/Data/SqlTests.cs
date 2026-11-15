// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Dapper;
using Grimoire.Functional.Data;

namespace Grimoire.UnitTests.Functional.Data;

public sealed class SqlTests
{
    [Fact]
    public void ReadAndWrite_SayWhichTheyAre()
    {
        Sql.Read("A.Read", "SELECT 1").IsRead.ShouldBeTrue();
        Sql.Write("A.Write", "DELETE FROM x").IsRead.ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void AStatement_NeedsAName(string name) =>
        Should.Throw<ArgumentException>(() => Sql.Read(name, "SELECT 1")).Message
            .ShouldStartWith("A statement needs a name, such as \"Warn.InsertSin\".");

    [Fact]
    public void AStatement_NeedsText() =>
        Should.Throw<ArgumentException>(() => Sql.Write("A.Write", " ")).Message
            .ShouldStartWith("Statement A.Write has no text.");

    [Fact]
    public void ParameterNames_FromAnAnonymousObject() =>
        Sql.Read("A", "SELECT @Guild, @User", new { Guild = 1, User = 2 }).ParameterNames
            .ShouldBe(["Guild", "User"]);

    [Fact]
    public void ParameterNames_FromDynamicParameters()
    {
        var parameters = new DynamicParameters();
        parameters.Add("Guild", 1);

        Sql.Read("A", "SELECT @Guild", parameters).ParameterNames.ShouldBe(["Guild"]);
    }

    [Fact]
    public void ParameterNames_WhenThereAreNone() => Sql.Read("A", "SELECT 1").ParameterNames.ShouldBeEmpty();
}
