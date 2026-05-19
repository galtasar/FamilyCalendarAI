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

    private static CalendarEvent MakeEvent(string title, string familyMember, DateTimeOffset start) => new()
    {
        Id = Guid.NewGuid(),
        EmailId = Guid.NewGuid(),
        Title = title,
        FamilyMemberName = familyMember,
        StartTime = start,
        Status = EventStatus.Created,
        CreatedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task IsDuplicate_ExactMatch_ReturnsTrue()
    {
        var start = new DateTimeOffset(2026, 9, 14, 18, 0, 0, TimeSpan.FromHours(2));
        var existing = MakeEvent("Föräldramöte klass 5", "Vera", start);

        _repoMock.Setup(r => r.GetByDateRangeAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), "Vera", It.IsAny<CancellationToken>()))
                 .ReturnsAsync([existing]);

        var candidate = MakeEvent("Föräldramöte klass 5", "Vera", start);
        var result = await CreateSut().IsDuplicateAsync(candidate);

        Assert.True(result);
    }

    [Fact]
    public async Task IsDuplicate_FuzzyTitleMatch_ReturnsTrue()
    {
        var start = new DateTimeOffset(2026, 9, 14, 18, 0, 0, TimeSpan.FromHours(2));
        var existing = MakeEvent("Föräldramöte klass 5", "Vera", start);

        _repoMock.Setup(r => r.GetByDateRangeAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), "Vera", It.IsAny<CancellationToken>()))
                 .ReturnsAsync([existing]);

        var candidate = MakeEvent("Föräldramöte i klass 5", "Vera", start); // slightly different title
        var result = await CreateSut().IsDuplicateAsync(candidate);

        Assert.True(result);
    }

    [Fact]
    public async Task IsDuplicate_DifferentFamilyMember_ReturnsFalse()
    {
        var start = new DateTimeOffset(2026, 9, 14, 18, 0, 0, TimeSpan.FromHours(2));
        var existing = MakeEvent("Föräldramöte", "Vera", start);

        _repoMock.Setup(r => r.GetByDateRangeAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), "Tage", It.IsAny<CancellationToken>()))
                 .ReturnsAsync([]);

        var candidate = MakeEvent("Föräldramöte", "Tage", start);
        var result = await CreateSut().IsDuplicateAsync(candidate);

        Assert.False(result);
    }

    [Fact]
    public async Task IsDuplicate_NoExistingEvents_ReturnsFalse()
    {
        _repoMock.Setup(r => r.GetByDateRangeAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync([]);

        var candidate = MakeEvent("Simskola", "Sixten", DateTimeOffset.UtcNow.AddDays(3));
        var result = await CreateSut().IsDuplicateAsync(candidate);

        Assert.False(result);
    }

    [Fact]
    public async Task IsDuplicate_TitleTooSimilarButDifferentDay_ReturnsFalse()
    {
        var start = new DateTimeOffset(2026, 9, 14, 18, 0, 0, TimeSpan.FromHours(2));
        var differentDay = start.AddDays(5);
        var existing = MakeEvent("Träning", "Tage", differentDay);

        _repoMock.Setup(r => r.GetByDateRangeAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), "Tage", It.IsAny<CancellationToken>()))
                 .ReturnsAsync([existing]);

        var candidate = MakeEvent("Träning", "Tage", start);
        var result = await CreateSut().IsDuplicateAsync(candidate);

        Assert.False(result);
    }
}
