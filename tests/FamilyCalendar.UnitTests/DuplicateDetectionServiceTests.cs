using FamilyCalendar.Core.Enums;
using FamilyCalendar.Core.Interfaces;
using FamilyCalendar.Core.Models;
using FamilyCalendar.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FamilyCalendar.UnitTests;

public class DuplicateDetectionServiceTests
{
    private readonly Mock<IEventRepository> _repoMock = new();

    private DuplicateDetectionService CreateSut() =>
        new(_repoMock.Object, NullLogger<DuplicateDetectionService>.Instance);

    private static CalendarEvent MakeEvent(string title, string familyMember, DateTimeOffset start, bool hasTime = true) => new()
    {
        Id = Guid.NewGuid(),
        EmailId = Guid.NewGuid(),
        Title = title,
        FamilyMemberName = familyMember,
        StartTime = start,
        HasTime = hasTime,
        Status = EventStatus.Created,
        CreatedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task FindMatch_ExactMatch_ReturnsExistingEvent()
    {
        var start = new DateTimeOffset(2026, 9, 14, 18, 0, 0, TimeSpan.FromHours(2));
        var existing = MakeEvent("Föräldramöte klass 5", "Vera", start);

        _repoMock.Setup(r => r.GetByDateRangeAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), "Vera", It.IsAny<CancellationToken>()))
                 .ReturnsAsync([existing]);
        _repoMock.Setup(r => r.GetByDateRangeAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), null, It.IsAny<CancellationToken>()))
                 .ReturnsAsync([existing]);

        var candidate = MakeEvent("Föräldramöte klass 5", "Vera", start);
        var result = await CreateSut().FindMatchAsync(candidate);

        Assert.NotNull(result);
        Assert.Equal(existing.Id, result.Id);
    }

    [Fact]
    public async Task FindMatch_FuzzyTitleMatch_ReturnsExistingEvent()
    {
        var start = new DateTimeOffset(2026, 9, 14, 18, 0, 0, TimeSpan.FromHours(2));
        var existing = MakeEvent("Föräldramöte klass 5", "Vera", start);

        _repoMock.Setup(r => r.GetByDateRangeAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), "Vera", It.IsAny<CancellationToken>()))
                 .ReturnsAsync([existing]);
        _repoMock.Setup(r => r.GetByDateRangeAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), null, It.IsAny<CancellationToken>()))
                 .ReturnsAsync([existing]);

        var candidate = MakeEvent("Föräldramöte i klass 5", "Vera", start); // slightly different title
        var result = await CreateSut().FindMatchAsync(candidate);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task FindMatch_DifferentFamilyMember_NoSimilarTitleForCrossMember_ReturnsNull()
    {
        var start = new DateTimeOffset(2026, 9, 14, 18, 0, 0, TimeSpan.FromHours(2));

        _repoMock.Setup(r => r.GetByDateRangeAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), "Tage", It.IsAny<CancellationToken>()))
                 .ReturnsAsync([]);
        _repoMock.Setup(r => r.GetByDateRangeAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), null, It.IsAny<CancellationToken>()))
                 .ReturnsAsync([]);

        var candidate = MakeEvent("Föräldramöte", "Tage", start);
        var result = await CreateSut().FindMatchAsync(candidate);

        Assert.Null(result);
    }

    [Fact]
    public async Task FindMatch_NoExistingEvents_ReturnsNull()
    {
        _repoMock.Setup(r => r.GetByDateRangeAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync([]);

        var candidate = MakeEvent("Simskola", "Sixten", DateTimeOffset.UtcNow.AddDays(3));
        var result = await CreateSut().FindMatchAsync(candidate);

        Assert.Null(result);
    }

    [Fact]
    public async Task FindMatch_SimilarTitleButDifferentDay_ReturnsNull()
    {
        var start = new DateTimeOffset(2026, 9, 14, 18, 0, 0, TimeSpan.FromHours(2));
        var differentDay = start.AddDays(5);
        var existing = MakeEvent("Träning", "Tage", differentDay);

        _repoMock.Setup(r => r.GetByDateRangeAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), "Tage", It.IsAny<CancellationToken>()))
                 .ReturnsAsync([existing]);
        _repoMock.Setup(r => r.GetByDateRangeAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), null, It.IsAny<CancellationToken>()))
                 .ReturnsAsync([existing]);

        var candidate = MakeEvent("Träning", "Tage", start);
        var result = await CreateSut().FindMatchAsync(candidate);

        Assert.Null(result);
    }

    // ── Fix 2: member overlap matching ──────────────────────────────────────────

    [Fact]
    public async Task FindMatch_CandidateSingleMember_MatchesExistingJoinedEvent()
    {
        var start = new DateTimeOffset(2026, 6, 13, 9, 0, 0, TimeSpan.Zero);
        var existing = MakeEvent("Skolavslutning", "Vera, Tage", start);

        // Repository returns the joined event when queried for "Vera"
        _repoMock.Setup(r => r.GetByDateRangeAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), "Vera", It.IsAny<CancellationToken>()))
                 .ReturnsAsync([existing]);
        _repoMock.Setup(r => r.GetByDateRangeAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), null, It.IsAny<CancellationToken>()))
                 .ReturnsAsync([existing]);

        var candidate = MakeEvent("Skolavslutning", "Vera", start);
        var result = await CreateSut().FindMatchAsync(candidate);

        Assert.NotNull(result);
        Assert.Equal(existing.Id, result.Id);
    }

    [Fact]
    public async Task FindMatch_CandidateJoinedMember_MatchesExistingSingleMember()
    {
        var start = new DateTimeOffset(2026, 6, 13, 9, 0, 0, TimeSpan.Zero);
        var existing = MakeEvent("Skolavslutning", "Vera", start);

        _repoMock.Setup(r => r.GetByDateRangeAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), "Vera", It.IsAny<CancellationToken>()))
                 .ReturnsAsync([existing]);
        _repoMock.Setup(r => r.GetByDateRangeAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), "Tage", It.IsAny<CancellationToken>()))
                 .ReturnsAsync([]);
        _repoMock.Setup(r => r.GetByDateRangeAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), null, It.IsAny<CancellationToken>()))
                 .ReturnsAsync([existing]);

        var candidate = MakeEvent("Skolavslutning", "Vera, Tage", start);
        var result = await CreateSut().FindMatchAsync(candidate);

        Assert.NotNull(result);
        Assert.Equal(existing.Id, result.Id);
    }

    [Fact]
    public async Task FindMatch_MemberOrderIndependent_ReturnsMatch()
    {
        var start = new DateTimeOffset(2026, 6, 13, 9, 0, 0, TimeSpan.Zero);
        var existing = MakeEvent("Studiedag", "Vera, Tage", start);

        _repoMock.Setup(r => r.GetByDateRangeAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), "Tage", It.IsAny<CancellationToken>()))
                 .ReturnsAsync([existing]);
        _repoMock.Setup(r => r.GetByDateRangeAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), "Vera", It.IsAny<CancellationToken>()))
                 .ReturnsAsync([existing]);
        _repoMock.Setup(r => r.GetByDateRangeAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), null, It.IsAny<CancellationToken>()))
                 .ReturnsAsync([existing]);

        var candidate = MakeEvent("Studiedag", "Tage, Vera", start); // reversed order
        var result = await CreateSut().FindMatchAsync(candidate);

        Assert.NotNull(result);
        Assert.Equal(existing.Id, result.Id);
    }

    // ── Fix 3: cross-member search ───────────────────────────────────────────────

    [Fact]
    public async Task FindMatch_CrossMember_AllDay_ReturnsExistingEvent()
    {
        var start = new DateTimeOffset(2026, 6, 13, 0, 0, 0, TimeSpan.Zero);
        var existing = MakeEvent("Skolavslutning", "Vera", start, hasTime: false);

        _repoMock.Setup(r => r.GetByDateRangeAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), "Tage", It.IsAny<CancellationToken>()))
                 .ReturnsAsync([]); // no Tage events
        _repoMock.Setup(r => r.GetByDateRangeAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), null, It.IsAny<CancellationToken>()))
                 .ReturnsAsync([existing]); // but cross-member search finds Vera's event

        var candidate = MakeEvent("Skolavslutning", "Tage", start, hasTime: false);
        var result = await CreateSut().FindMatchAsync(candidate);

        Assert.NotNull(result);
        Assert.Equal(existing.Id, result.Id);
    }

    [Fact]
    public async Task FindMatch_CrossMember_TimedWithin2h_ReturnsExistingEvent()
    {
        var existingStart = new DateTimeOffset(2026, 6, 13, 9, 0, 0, TimeSpan.Zero);
        var candidateStart = existingStart.AddMinutes(30); // 30 minutes apart — within threshold
        var existing = MakeEvent("Skolavslutning", "Vera", existingStart, hasTime: true);

        _repoMock.Setup(r => r.GetByDateRangeAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), "Tage", It.IsAny<CancellationToken>()))
                 .ReturnsAsync([]);
        _repoMock.Setup(r => r.GetByDateRangeAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), null, It.IsAny<CancellationToken>()))
                 .ReturnsAsync([existing]);

        var candidate = MakeEvent("Skolavslutning", "Tage", candidateStart, hasTime: true);
        var result = await CreateSut().FindMatchAsync(candidate);

        Assert.NotNull(result);
        Assert.Equal(existing.Id, result.Id);
    }

    [Fact]
    public async Task FindMatch_CrossMember_TimedBeyond2h_ReturnsNull()
    {
        var existingStart = new DateTimeOffset(2026, 6, 13, 9, 0, 0, TimeSpan.Zero);
        var candidateStart = existingStart.AddHours(3); // 3 hours apart — beyond threshold
        var existing = MakeEvent("Träning", "Vera", existingStart, hasTime: true);

        _repoMock.Setup(r => r.GetByDateRangeAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), "Tage", It.IsAny<CancellationToken>()))
                 .ReturnsAsync([]);
        _repoMock.Setup(r => r.GetByDateRangeAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), null, It.IsAny<CancellationToken>()))
                 .ReturnsAsync([existing]);

        var candidate = MakeEvent("Träning", "Tage", candidateStart, hasTime: true);
        var result = await CreateSut().FindMatchAsync(candidate);

        Assert.Null(result);
    }
}