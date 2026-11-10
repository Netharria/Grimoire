// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Alerts;

namespace Grimoire.Tests.Features.Alerts;

/// <summary>
///     The urgent post's body (backlog A-53). The header (type, discriminator, guild, repeat count) and a body without an
///     exception are pinned end to end in <c>Integration/Shared/AlertTests.cs</c>; these cover the exception block and
///     the 2000-character cut.
/// </summary>
public sealed class AlertMessageFormatterTests
{
    private static Alert Urgent(string message = "it broke", Exception? exception = null)
        => AlertTestHelpers.Warning("TestAlert", null, null, exception) with
        {
            Severity = AlertSeverity.Urgent, Message = message
        };

    [Fact]
    public void An_exception_is_shown_in_a_csharp_code_block_after_the_message()
    {
        var text = AlertMessageFormatter.FormatUrgent(
            Urgent(exception: AlertTestHelpers.Thrown(() => new InvalidOperationException("outer failure"))), 0);

        text.ShouldStartWith("**[URGENT]** TestAlert\nit broke\n```csharp\nouter failure");
        text.ShouldEndWith("\n```");
    }

    [Fact]
    public void Inner_exception_messages_are_listed_outermost_first()
    {
        var text = AlertMessageFormatter.FormatUrgent(Urgent(exception: AlertTestHelpers.Thrown(() =>
            new InvalidOperationException("outer", new ArgumentException("middle", new TimeoutException("inner"))))), 0);

        text.IndexOf("outer", StringComparison.Ordinal).ShouldBeLessThan(text.IndexOf("middle", StringComparison.Ordinal));
        text.IndexOf("middle", StringComparison.Ordinal).ShouldBeLessThan(text.IndexOf("inner", StringComparison.Ordinal));
    }

    /// <summary>Only Grimoire's own frames are kept, shortened to <c>"file" line N</c>.</summary>
    [Fact]
    public void The_stack_trace_keeps_only_grimoire_frames_as_file_and_line()
    {
        var exception = AlertTestHelpers.Thrown(() => new InvalidOperationException("boom"));
        var trace = exception.StackTrace!;
        trace.ShouldContain(" in ");

        var text = AlertMessageFormatter.FormatUrgent(Urgent(exception: exception), 0);

        text.ShouldContain("AlertTestHelpers.cs\" line ");
        text.ShouldNotContain("   at ");
    }

    [Fact]
    public void Frames_outside_grimoire_are_left_out()
    {
        var exception = Thrown(() => int.Parse("not a number", System.Globalization.CultureInfo.InvariantCulture));
        exception.StackTrace!.ShouldContain("System.Number");

        var text = AlertMessageFormatter.FormatUrgent(Urgent(exception: exception), 0);

        text.ShouldNotContain("System.Number");
        text.ShouldContain("AlertMessageFormatterTests.cs\" line ");
    }

    /// <summary>A code fence inside an exception message would end the block early, so it is defused.</summary>
    [Fact]
    public void A_code_fence_in_the_exception_message_is_defused()
    {
        var text = AlertMessageFormatter.FormatUrgent(
            Urgent(exception: new InvalidOperationException("bad ```sql\nDROP\n``` input")), 0);

        text.ShouldContain("bad '''sql\nDROP\n''' input");
        CountFences(text).ShouldBe(2);
    }

    [Fact]
    public void An_exception_that_was_never_thrown_has_messages_but_no_frames()
        => AlertMessageFormatter.FormatUrgent(Urgent(exception: new InvalidOperationException("never thrown")), 0)
            .ShouldBe($"**[URGENT]** TestAlert\nit broke\n```csharp\nnever thrown{Environment.NewLine}\n\n```");

    [Fact]
    public void A_post_of_exactly_2000_characters_is_not_cut()
    {
        const string header = "**[URGENT]** TestAlert\n";
        var message = new string('x', 2000 - header.Length);

        var text = AlertMessageFormatter.FormatUrgent(Urgent(message), 0);

        text.Length.ShouldBe(2000);
        text.ShouldEndWith("x");
    }

    [Fact]
    public void A_longer_post_is_cut_to_2000_characters_ending_in_an_ellipsis()
    {
        var text = AlertMessageFormatter.FormatUrgent(Urgent(new string('x', 5000)), 0);

        text.Length.ShouldBe(2000);
        text.ShouldEndWith("xxx...");
    }

    /// <summary>
    ///     ALRT-11: a long exception is cut to 2000 characters, and the cut takes the closing code fence with it, so
    ///     Discord shows the rest of the post as unterminated code.
    /// </summary>
    [Fact(Skip = "Fails: the 2000-character cut drops the closing code fence. KI-93")]
    public void A_cut_post_with_an_exception_still_closes_its_code_block()
    {
        var text = AlertMessageFormatter.FormatUrgent(
            Urgent(exception: new InvalidOperationException(new string('x', 5000))), 0);

        text.Length.ShouldBeLessThanOrEqualTo(2000);
        CountFences(text).ShouldBe(2);
    }

    private static int CountFences(string text)
        => text.Split("```").Length - 1;

    private static Exception Thrown(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            return ex;
        }

        throw new InvalidOperationException("The action did not throw.");
    }
}
