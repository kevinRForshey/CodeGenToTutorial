using Paarde.Core.Helpers;

namespace Paarde.Core.Tests.Helpers;

public class MentionTriggerDetectorTests
{
    [Fact]
    public void FindActiveTrigger_AtStartOfText_ReturnsEmptyQuery()
    {
        var trigger = MentionTriggerDetector.FindActiveTrigger("@", caretIndex: 1);

        Assert.Equal(new MentionTrigger(TriggerStart: 0, Query: ""), trigger);
    }

    [Fact]
    public void FindActiveTrigger_MidWordAfterWhitespace_ReturnsTheTypedQuery()
    {
        var trigger = MentionTriggerDetector.FindActiveTrigger("Hello @fo", caretIndex: 9);

        Assert.Equal(new MentionTrigger(TriggerStart: 6, Query: "fo"), trigger);
    }

    [Fact]
    public void FindActiveTrigger_AtSignPrecededByNonWhitespace_ReturnsNull()
    {
        var trigger = MentionTriggerDetector.FindActiveTrigger("email@domain", caretIndex: 12);

        Assert.Null(trigger);
    }

    [Fact]
    public void FindActiveTrigger_NoAtSignBeforeCaret_ReturnsNull()
    {
        var trigger = MentionTriggerDetector.FindActiveTrigger("just some text", caretIndex: 14);

        Assert.Null(trigger);
    }

    [Fact]
    public void FindActiveTrigger_MentionAlreadyClosedByWhitespace_ReturnsNull()
    {
        var trigger = MentionTriggerDetector.FindActiveTrigger("@foo bar", caretIndex: 8);

        Assert.Null(trigger);
    }

    [Fact]
    public void FindActiveTrigger_CaretBeforeTheMention_ReturnsNull()
    {
        var trigger = MentionTriggerDetector.FindActiveTrigger("@foo", caretIndex: 0);

        Assert.Null(trigger);
    }

    [Fact]
    public void FindActiveTrigger_CaretMidwayThroughTheQuery_ReturnsOnlyTheTypedPrefix()
    {
        var trigger = MentionTriggerDetector.FindActiveTrigger("@foobar", caretIndex: 4);

        Assert.Equal(new MentionTrigger(TriggerStart: 0, Query: "foo"), trigger);
    }
}
