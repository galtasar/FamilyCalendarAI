using FamilyCalendar.Core.Models;
using FamilyCalendar.Core.Services;

namespace FamilyCalendar.UnitTests;

public class WeekdayCorrectorTests
{
    private static readonly TimeSpan Cet = TimeSpan.FromHours(1);

    [Fact]
    public void Correct_FridayButEmailSaysThursday_MovesBackOneDay()
    {
        // Production case: "Utklädningsmaskerad ... på torsdag" dated fredag 13 nov 2026.
        var draft = new AiEventDraft
        {
            Start = new DateTimeOffset(2026, 11, 13, 0, 0, 0, Cet),
            HasTime = false,
            Weekday = "torsdag"
        };

        Assert.True(WeekdayCorrector.Correct(draft));
        Assert.Equal(new DateTimeOffset(2026, 11, 12, 0, 0, 0, Cet), draft.Start);
        Assert.True(draft.RequiresManualReview);
    }

    [Fact]
    public void Correct_ShiftsEndTogetherWithStart()
    {
        var draft = new AiEventDraft
        {
            Start = new DateTimeOffset(2026, 11, 10, 18, 0, 0, Cet), // tisdag
            End = new DateTimeOffset(2026, 11, 10, 19, 0, 0, Cet),
            HasTime = true,
            Weekday = "Onsdag"
        };

        Assert.True(WeekdayCorrector.Correct(draft));
        Assert.Equal(new DateTimeOffset(2026, 11, 11, 18, 0, 0, Cet), draft.Start);
        Assert.Equal(new DateTimeOffset(2026, 11, 11, 19, 0, 0, Cet), draft.End);
    }

    [Fact]
    public void Correct_PicksNearestMatchingDay()
    {
        var draft = new AiEventDraft
        {
            Start = new DateTimeOffset(2026, 11, 9, 0, 0, 0, Cet), // måndag
            Weekday = "söndag"
        };

        Assert.True(WeekdayCorrector.Correct(draft));
        Assert.Equal(new DateTimeOffset(2026, 11, 8, 0, 0, 0, Cet), draft.Start);
    }

    [Theory]
    [InlineData("torsdag")]
    [InlineData("torsdags")]
    [InlineData("Thursday")]
    public void Correct_MatchingWeekday_LeavesDraftUntouched(string weekday)
    {
        var start = new DateTimeOffset(2026, 11, 12, 0, 0, 0, Cet); // torsdag
        var draft = new AiEventDraft { Start = start, Weekday = weekday };

        Assert.False(WeekdayCorrector.Correct(draft));
        Assert.Equal(start, draft.Start);
        Assert.False(draft.RequiresManualReview);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("imorgon")]
    public void Correct_NoOrUnknownWeekday_LeavesDraftUntouched(string? weekday)
    {
        var start = new DateTimeOffset(2026, 11, 13, 0, 0, 0, Cet);
        var draft = new AiEventDraft { Start = start, Weekday = weekday };

        Assert.False(WeekdayCorrector.Correct(draft));
        Assert.Equal(start, draft.Start);
    }
}
