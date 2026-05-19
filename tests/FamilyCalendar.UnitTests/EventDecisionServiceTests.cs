using FamilyCalendar.Core.Enums;
using FamilyCalendar.Core.Interfaces;
using FamilyCalendar.Core.Models;
using FamilyCalendar.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using EmailModel = FamilyCalendar.Core.Models.Email;

namespace FamilyCalendar.UnitTests;

public class EventDecisionServiceTests
{
    private readonly Mock<IEventRepository> _eventRepoMock = new();
    private readonly Mock<IDuplicateDetectionService> _dupMock = new();

    private EventDecisionService CreateSut() =>
        new(_eventRepoMock.Object, _dupMock.Object, NullLogger<EventDecisionService>.Instance);

    private static EmailModel MakeEmail() => new()
    {
        Id = Guid.NewGuid(),
        MessageId = "msg-1",
        Sender = "skola@vattholmaskolan.se",
        Subject = "Föräldramöte",
        Body = "Hej!",
        ReceivedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task ProcessAsync_IrrelevantEmail_ReturnsEmpty()
    {
        var result = new AiAnalysisResult { Relevant = false, RequiresCalendarEvent = false };
        var events = await CreateSut().ProcessAsync(MakeEmail(), result);
        Assert.Empty(events);
        _eventRepoMock.Verify(r => r.AddAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_HighConfidence_AutoApproves()
    {
        _dupMock.Setup(d => d.IsDuplicateAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
        _eventRepoMock.Setup(r => r.AddAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()))
                      .ReturnsAsync((CalendarEvent e, CancellationToken _) => e);

        var result = new AiAnalysisResult
        {
            Relevant = true, RequiresCalendarEvent = true, Confidence = 0.95,
            FamilyMembers = ["Vera"], Title = "Föräldramöte",
            Start = DateTimeOffset.UtcNow.AddDays(7), HasTime = true
        };

        var events = await CreateSut().ProcessAsync(MakeEmail(), result);

        Assert.Single(events);
        Assert.False(events[0].NeedsReview);
        Assert.Equal(EventStatus.Approved, events[0].Status);
    }

    [Fact]
    public async Task ProcessAsync_LowConfidence_RequiresReview()
    {
        _eventRepoMock.Setup(r => r.AddAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()))
                      .ReturnsAsync((CalendarEvent e, CancellationToken _) => e);

        var result = new AiAnalysisResult
        {
            Relevant = true, RequiresCalendarEvent = true, Confidence = 0.65,
            FamilyMembers = ["Tage"], Title = "Träning",
            Start = DateTimeOffset.UtcNow.AddDays(3)
        };

        var events = await CreateSut().ProcessAsync(MakeEmail(), result);

        Assert.Single(events);
        Assert.True(events[0].NeedsReview);
        Assert.Equal(EventStatus.Pending, events[0].Status);
        // Duplicate check should NOT be called for review-flagged events
        _dupMock.Verify(d => d.IsDuplicateAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_RecurringEvent_RequiresReview()
    {
        _eventRepoMock.Setup(r => r.AddAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()))
                      .ReturnsAsync((CalendarEvent e, CancellationToken _) => e);

        var result = new AiAnalysisResult
        {
            Relevant = true, RequiresCalendarEvent = true, Confidence = 0.92,
            FamilyMembers = ["Sixten"], Title = "Simskola", IsRecurring = true,
            Start = DateTimeOffset.UtcNow.AddDays(5)
        };

        var events = await CreateSut().ProcessAsync(MakeEmail(), result);

        Assert.Single(events);
        Assert.True(events[0].NeedsReview);
    }

    [Fact]
    public async Task ProcessAsync_MultipleFamilyMembers_CreatesEventPerFamilyMember()
    {
        _dupMock.Setup(d => d.IsDuplicateAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
        _eventRepoMock.Setup(r => r.AddAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()))
                      .ReturnsAsync((CalendarEvent e, CancellationToken _) => e);

        var result = new AiAnalysisResult
        {
            Relevant = true, RequiresCalendarEvent = true, Confidence = 0.88,
            FamilyMembers = ["Vera", "Tage"], Title = "Studiedag",
            Start = DateTimeOffset.UtcNow.AddDays(10)
        };

        var events = await CreateSut().ProcessAsync(MakeEmail(), result);

        Assert.Equal(2, events.Count);
        Assert.Contains(events, e => e.FamilyMemberName == "Vera");
        Assert.Contains(events, e => e.FamilyMemberName == "Tage");
    }

    [Fact]
    public async Task ProcessAsync_Duplicate_SkipsEvent()
    {
        _dupMock.Setup(d => d.IsDuplicateAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

        var result = new AiAnalysisResult
        {
            Relevant = true, RequiresCalendarEvent = true, Confidence = 0.90,
            FamilyMembers = ["Folke"], Title = "Utflykt",
            Start = DateTimeOffset.UtcNow.AddDays(2), HasTime = true
        };

        var events = await CreateSut().ProcessAsync(MakeEmail(), result);

        Assert.Empty(events);
        _eventRepoMock.Verify(r => r.AddAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_MissingDate_RequiresReview()
    {
        _eventRepoMock.Setup(r => r.AddAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()))
                      .ReturnsAsync((CalendarEvent e, CancellationToken _) => e);

        var result = new AiAnalysisResult
        {
            Relevant = true, RequiresCalendarEvent = true, Confidence = 0.90,
            FamilyMembers = ["Vera"], Title = "Möte",
            Start = null  // No date
        };

        var events = await CreateSut().ProcessAsync(MakeEmail(), result);

        Assert.Single(events);
        Assert.True(events[0].NeedsReview);
    }

    [Fact]
    public async Task ProcessAsync_NoFamilyMembers_RequiresReview()
    {
        _eventRepoMock.Setup(r => r.AddAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()))
                      .ReturnsAsync((CalendarEvent e, CancellationToken _) => e);

        var result = new AiAnalysisResult
        {
            Relevant = true, RequiresCalendarEvent = true, Confidence = 0.92,
            FamilyMembers = [], Title = "Aktivitet",
            Start = DateTimeOffset.UtcNow.AddDays(4)
        };

        var events = await CreateSut().ProcessAsync(MakeEmail(), result);

        Assert.Empty(events);
    }
}
