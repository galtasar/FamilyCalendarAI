using FamilyCalendar.Core.Enums;
using FamilyCalendar.Core.Interfaces;
using FamilyCalendar.Core.Models;
using FamilyCalendar.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using EmailModel = FamilyCalendar.Core.Models.Email;

namespace FamilyCalendar.UnitTests;

public class EventDecisionServiceTests
{
    private readonly Mock<IEventRepository> _eventRepoMock = new();
    private readonly Mock<IDuplicateDetectionService> _dupMock = new();
    private readonly Mock<IDescriptionEvaluationService> _descEvalMock = new();

    private EventDecisionService CreateSut() =>
        new(_eventRepoMock.Object, _dupMock.Object, _descEvalMock.Object,
            Options.Create(new EventDecisionOptions()),
            NullLogger<EventDecisionService>.Instance);

    private static EmailModel MakeEmail() => new()
    {
        Id = Guid.NewGuid(),
        MessageId = "msg-1",
        Sender = "skola@vattholmaskolan.se",
        Subject = "Föräldramöte",
        Body = "Hej!",
        ReceivedAt = DateTimeOffset.UtcNow
    };

    private static AiAnalysisResult MakeResult(bool relevant, double confidence, params AiEventDraft[] events) =>
        new() { Relevant = relevant, Confidence = confidence, Events = [.. events] };

    private static CalendarEvent MakeExistingEvent(string title, string familyMember, DateTimeOffset start,
        string? calendarEventId = null, string? description = null, string? location = null,
        bool hasTime = true) => new()
    {
        Id = Guid.NewGuid(),
        EmailId = Guid.NewGuid(),
        Title = title,
        FamilyMemberName = familyMember,
        StartTime = start,
        Description = description,
        Location = location,
        CalendarEventId = calendarEventId,
        HasTime = hasTime,
        Status = calendarEventId != null ? EventStatus.Created : EventStatus.Pending,
        CreatedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task ProcessAsync_IrrelevantEmail_ReturnsEmpty()
    {
        var result = MakeResult(relevant: false, confidence: 1.0);
        var events = await CreateSut().ProcessAsync(MakeEmail(), result);
        Assert.Empty(events);
        _eventRepoMock.Verify(r => r.AddAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_RelevantButNoEvents_ReturnsEmpty()
    {
        var result = MakeResult(relevant: true, confidence: 1.0);
        var events = await CreateSut().ProcessAsync(MakeEmail(), result);
        Assert.Empty(events);
        _eventRepoMock.Verify(r => r.AddAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_HighConfidence_AutoApproves()
    {
        _dupMock.Setup(d => d.FindMatchAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((CalendarEvent?)null);
        _eventRepoMock.Setup(r => r.AddAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()))
                      .ReturnsAsync((CalendarEvent e, CancellationToken _) => e);

        var result = MakeResult(relevant: true, confidence: 0.95, new AiEventDraft
        {
            FamilyMembers = ["Vera"], Title = "Föräldramöte",
            Start = DateTimeOffset.UtcNow.AddDays(7), End = DateTimeOffset.UtcNow.AddDays(7).AddHours(1),
            HasTime = true
        });

        var events = await CreateSut().ProcessAsync(MakeEmail(), result);

        Assert.Single(events);
        Assert.False(events[0].NeedsReview);
        Assert.Equal(EventStatus.Approved, events[0].Status);
    }

    [Fact]
    public async Task ProcessAsync_LowConfidence_RequiresReview()
    {
        _dupMock.Setup(d => d.FindMatchAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((CalendarEvent?)null);
        _eventRepoMock.Setup(r => r.AddAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()))
                      .ReturnsAsync((CalendarEvent e, CancellationToken _) => e);

        var result = MakeResult(relevant: true, confidence: 0.65, new AiEventDraft
        {
            FamilyMembers = ["Tage"], Title = "Träning",
            Start = DateTimeOffset.UtcNow.AddDays(3), HasTime = true
        });

        var events = await CreateSut().ProcessAsync(MakeEmail(), result);

        Assert.Single(events);
        Assert.True(events[0].NeedsReview);
        Assert.Equal(EventStatus.Pending, events[0].Status);
    }

    [Fact]
    public async Task ProcessAsync_RecurringEvent_RequiresReview()
    {
        _dupMock.Setup(d => d.FindMatchAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((CalendarEvent?)null);
        _eventRepoMock.Setup(r => r.AddAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()))
                      .ReturnsAsync((CalendarEvent e, CancellationToken _) => e);

        var result = MakeResult(relevant: true, confidence: 0.92, new AiEventDraft
        {
            FamilyMembers = ["Sixten"], Title = "Simskola", IsRecurring = true,
            Start = DateTimeOffset.UtcNow.AddDays(5), HasTime = true
        });

        var events = await CreateSut().ProcessAsync(MakeEmail(), result);

        Assert.Single(events);
        Assert.True(events[0].NeedsReview);
    }

    [Fact]
    public async Task ProcessAsync_MultipleFamilyMembers_CreatesSingleEventWithJoinedNames()
    {
        _dupMock.Setup(d => d.FindMatchAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((CalendarEvent?)null);
        _eventRepoMock.Setup(r => r.AddAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()))
                      .ReturnsAsync((CalendarEvent e, CancellationToken _) => e);

        var result = MakeResult(relevant: true, confidence: 0.88, new AiEventDraft
        {
            FamilyMembers = ["Vera", "Tage"], Title = "Studiedag",
            Start = DateTimeOffset.UtcNow.AddDays(10), HasTime = true
        });

        var events = await CreateSut().ProcessAsync(MakeEmail(), result);

        Assert.Single(events);
        Assert.Equal("Vera, Tage", events[0].FamilyMemberName);
    }

    [Fact]
    public async Task ProcessAsync_MultipleEventDrafts_CreatesOnePerDraft()
    {
        _dupMock.Setup(d => d.FindMatchAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((CalendarEvent?)null);
        _eventRepoMock.Setup(r => r.AddAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()))
                      .ReturnsAsync((CalendarEvent e, CancellationToken _) => e);

        var result = MakeResult(relevant: true, confidence: 0.95,
            new AiEventDraft
            {
                FamilyMembers = ["Vera"], Title = "Skansenutflykt",
                Start = DateTimeOffset.UtcNow.AddDays(3), HasTime = true
            },
            new AiEventDraft
            {
                FamilyMembers = ["Tage"], Title = "Friidrottsdag",
                Start = DateTimeOffset.UtcNow.AddDays(5), HasTime = true
            });

        var events = await CreateSut().ProcessAsync(MakeEmail(), result);

        Assert.Equal(2, events.Count);
        Assert.Contains(events, e => e.Title == "Skansenutflykt");
        Assert.Contains(events, e => e.Title == "Friidrottsdag");
    }

    [Fact]
    public async Task ProcessAsync_PureDuplicate_SkipsEvent()
    {
        var start = DateTimeOffset.UtcNow.AddDays(2);
        var existing = MakeExistingEvent("Utflykt", "Folke", start, description: "Inga nyheter");

        _dupMock.Setup(d => d.FindMatchAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(existing);
        // AI confirms: no new information
        _descEvalMock.Setup(d => d.MergeAsync(It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((string?)null);

        var result = MakeResult(relevant: true, confidence: 0.90, new AiEventDraft
        {
            FamilyMembers = ["Folke"], Title = "Utflykt",
            Start = start, HasTime = true,
            Summary = "Inga nyheter" // same description — pure duplicate
        });

        var events = await CreateSut().ProcessAsync(MakeEmail(), result);

        Assert.Empty(events);
        _eventRepoMock.Verify(r => r.AddAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        _eventRepoMock.Verify(r => r.UpdateAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_MatchWithNewDescription_UpdatesExistingPendingEvent_ReturnsNull()
    {
        var start = DateTimeOffset.UtcNow.AddDays(5);
        var existing = MakeExistingEvent("Utflykt", "Vera", start, calendarEventId: null, description: "Grundinfo");

        _dupMock.Setup(d => d.FindMatchAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(existing);
        // AI confirms: new info present, returns merged description
        _descEvalMock.Setup(d => d.MergeAsync(It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync("Grundinfo\n\nTa med gympaskor");
        _eventRepoMock.Setup(r => r.UpdateAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()))
                      .Returns(Task.CompletedTask);

        var result = MakeResult(relevant: true, confidence: 0.92, new AiEventDraft
        {
            FamilyMembers = ["Vera"], Title = "Utflykt",
            Start = start, HasTime = true,
            Summary = "Ta med gympaskor" // new info
        });

        var events = await CreateSut().ProcessAsync(MakeEmail(), result);

        // Pending event — no return, just updated in DB
        Assert.Empty(events);
        _eventRepoMock.Verify(r => r.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Contains("Ta med gympaskor", existing.Description);
    }

    [Fact]
    public async Task ProcessAsync_MatchWithNewDescriptionAndCalendarEventId_ReturnsEventForCalendarUpdate()
    {
        var start = DateTimeOffset.UtcNow.AddDays(5);
        var existing = MakeExistingEvent("Utflykt", "Vera", start, calendarEventId: "gcal-123", description: "Grundinfo");

        _dupMock.Setup(d => d.FindMatchAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(existing);
        _descEvalMock.Setup(d => d.MergeAsync(It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync("Grundinfo\n\nTa med gympaskor");
        _eventRepoMock.Setup(r => r.UpdateAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()))
                      .Returns(Task.CompletedTask);

        var result = MakeResult(relevant: true, confidence: 0.92, new AiEventDraft
        {
            FamilyMembers = ["Vera"], Title = "Utflykt",
            Start = start, HasTime = true,
            Summary = "Ta med gympaskor"
        });

        var events = await CreateSut().ProcessAsync(MakeEmail(), result);

        // Already in Google Calendar — returned so caller can push update
        Assert.Single(events);
        Assert.Equal("gcal-123", events[0].CalendarEventId);
        Assert.Contains("Ta med gympaskor", events[0].Description);
    }

    [Fact]
    public async Task ProcessAsync_MatchWithNewFamilyMember_MergesMemberNames()
    {
        var start = DateTimeOffset.UtcNow.AddDays(3);
        var existing = MakeExistingEvent("Studiedag", "Vera", start, calendarEventId: "gcal-456");

        _dupMock.Setup(d => d.FindMatchAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(existing);
        _descEvalMock.Setup(d => d.MergeAsync(It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((string?)null);
        _eventRepoMock.Setup(r => r.UpdateAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()))
                      .Returns(Task.CompletedTask);

        var result = MakeResult(relevant: true, confidence: 0.95, new AiEventDraft
        {
            FamilyMembers = ["Vera", "Tage"], Title = "Studiedag",
            Start = start, HasTime = true
        });

        var events = await CreateSut().ProcessAsync(MakeEmail(), result);

        Assert.Single(events);
        Assert.Contains("Tage", events[0].FamilyMemberName);
        Assert.Contains("Vera", events[0].FamilyMemberName);
    }

    [Fact]
    public async Task ProcessAsync_MissingDate_RequiresReview()
    {
        _dupMock.Setup(d => d.FindMatchAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((CalendarEvent?)null);
        _eventRepoMock.Setup(r => r.AddAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()))
                      .ReturnsAsync((CalendarEvent e, CancellationToken _) => e);

        var result = MakeResult(relevant: true, confidence: 0.90, new AiEventDraft
        {
            FamilyMembers = ["Vera"], Title = "Möte", Start = null, HasTime = false
        });

        var events = await CreateSut().ProcessAsync(MakeEmail(), result);

        Assert.Single(events);
        Assert.True(events[0].NeedsReview);
    }

    [Fact]
    public async Task ProcessAsync_NoFamilyMembers_SkipsEvent()
    {
        var result = MakeResult(relevant: true, confidence: 0.92, new AiEventDraft
        {
            FamilyMembers = [], Title = "Aktivitet",
            Start = DateTimeOffset.UtcNow.AddDays(4), HasTime = true
        });

        var events = await CreateSut().ProcessAsync(MakeEmail(), result);

        Assert.Empty(events);
        _eventRepoMock.Verify(r => r.AddAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_CancelAction_NoMatch_ReturnsEmpty()
    {
        _dupMock.Setup(d => d.FindMatchAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((CalendarEvent?)null);

        var result = MakeResult(relevant: true, confidence: 0.95, new AiEventDraft
        {
            Action = "cancel",
            FamilyMembers = ["Vera"], Title = "Utflykt",
            Start = DateTimeOffset.UtcNow.AddDays(3), HasTime = true
        });

        var events = await CreateSut().ProcessAsync(MakeEmail(), result);

        Assert.Empty(events);
        _eventRepoMock.Verify(r => r.UpdateAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_CancelAction_PendingMatch_MarksRejectedReturnsNull()
    {
        var start = DateTimeOffset.UtcNow.AddDays(3);
        var existing = MakeExistingEvent("Utflykt", "Vera", start, calendarEventId: null);

        _dupMock.Setup(d => d.FindMatchAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(existing);
        _eventRepoMock.Setup(r => r.UpdateAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()))
                      .Returns(Task.CompletedTask);

        var result = MakeResult(relevant: true, confidence: 0.95, new AiEventDraft
        {
            Action = "cancel",
            FamilyMembers = ["Vera"], Title = "Utflykt",
            Start = start, HasTime = true
        });

        var events = await CreateSut().ProcessAsync(MakeEmail(), result);

        // Pending event — marked Rejected in DB, not returned (no Google Calendar to delete from)
        Assert.Empty(events);
        Assert.Equal(EventStatus.Rejected, existing.Status);
        _eventRepoMock.Verify(r => r.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_CancelAction_CreatedMatch_ReturnsEventForCalendarDeletion()
    {
        var start = DateTimeOffset.UtcNow.AddDays(3);
        var existing = MakeExistingEvent("Utflykt", "Vera", start, calendarEventId: "gcal-789");

        _dupMock.Setup(d => d.FindMatchAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(existing);
        _eventRepoMock.Setup(r => r.UpdateAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()))
                      .Returns(Task.CompletedTask);

        var result = MakeResult(relevant: true, confidence: 0.95, new AiEventDraft
        {
            Action = "cancel",
            FamilyMembers = ["Vera"], Title = "Utflykt",
            Start = start, HasTime = true
        });

        var events = await CreateSut().ProcessAsync(MakeEmail(), result);

        // Event is in Google Calendar — returned with Rejected status so caller can delete it
        Assert.Single(events);
        Assert.Equal(EventStatus.Rejected, events[0].Status);
        Assert.Equal("gcal-789", events[0].CalendarEventId);
    }

    [Fact]
    public async Task ProcessAsync_CrossMemberMerge_FlagsNeedsReview()
    {
        var start = DateTimeOffset.UtcNow.AddDays(5);
        // Existing event only has Vera
        var existing = MakeExistingEvent("Skolavslutning", "Vera", start, calendarEventId: "gcal-sko");

        _dupMock.Setup(d => d.FindMatchAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(existing); // cross-member match returned
        _descEvalMock.Setup(d => d.MergeAsync(It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((string?)null);
        _eventRepoMock.Setup(r => r.UpdateAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()))
                      .Returns(Task.CompletedTask);

        // New email adds Tage to the same event
        var result = MakeResult(relevant: true, confidence: 0.95, new AiEventDraft
        {
            FamilyMembers = ["Tage"], Title = "Skolavslutning",
            Start = start, HasTime = true
        });

        var events = await CreateSut().ProcessAsync(MakeEmail(), result);

        Assert.Single(events);
        Assert.True(events[0].NeedsReview);
        Assert.Contains("Tage", events[0].FamilyMemberName);
        Assert.Contains("Vera", events[0].FamilyMemberName);
    }

    [Fact]
    public async Task ProcessAsync_MorePreciseTime_UpdatesStartTimeOnExistingEvent()
    {
        var dateOnly = new DateTimeOffset(2026, 6, 13, 0, 0, 0, TimeSpan.Zero);
        var existing = MakeExistingEvent("Utflykt", "Vera", dateOnly, calendarEventId: "gcal-trip", hasTime: false);

        _dupMock.Setup(d => d.FindMatchAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(existing);
        _descEvalMock.Setup(d => d.MergeAsync(It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((string?)null);
        _eventRepoMock.Setup(r => r.UpdateAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()))
                      .Returns(Task.CompletedTask);

        var preciseStart = new DateTimeOffset(2026, 6, 13, 9, 0, 0, TimeSpan.Zero);
        var preciseEnd = new DateTimeOffset(2026, 6, 13, 15, 0, 0, TimeSpan.Zero);
        var result = MakeResult(relevant: true, confidence: 0.95, new AiEventDraft
        {
            FamilyMembers = ["Vera"], Title = "Utflykt",
            Start = preciseStart, End = preciseEnd, HasTime = true
        });

        await CreateSut().ProcessAsync(MakeEmail(), result);

        Assert.True(existing.HasTime);
        Assert.Equal(preciseStart.ToUniversalTime(), existing.StartTime);
        Assert.Equal(preciseEnd.ToUniversalTime(), existing.EndTime);
    }
}
