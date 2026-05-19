using System.Text.Json;
using FamilyCalendar.Core.Models;

namespace FamilyCalendar.UnitTests;

public class AiAnalysisResultDeserializationTests
{
    [Fact]
    public void Deserialize_FullResult_MapsAllFields()
    {
        var json = """
            {
              "relevant": true,
              "confidence": 0.93,
              "children": ["Vera"],
              "event_type": "Föräldramöte",
              "title": "Föräldramöte klass 5",
              "start": "2026-09-14T18:00:00+02:00",
              "end": "2026-09-14T19:30:00+02:00",
              "location": "Vattholmaskolan",
              "requires_calendar_event": true,
              "requires_manual_review": false,
              "is_recurring": false,
              "summary": "Föräldramöte för Vera"
            }
            """;

        var result = JsonSerializer.Deserialize<AiAnalysisResult>(json);

        Assert.NotNull(result);
        Assert.True(result.Relevant);
        Assert.Equal(0.93, result.Confidence);
        Assert.Equal(["Vera"], result.FamilyMembers);
        Assert.Equal("Föräldramöte klass 5", result.Title);
        Assert.Equal("Vattholmaskolan", result.Location);
        Assert.True(result.RequiresCalendarEvent);
        Assert.False(result.RequiresManualReview);
        Assert.False(result.IsRecurring);
        Assert.NotNull(result.Start);
    }

    [Fact]
    public void Deserialize_IrrelevantEmail_ReturnsNotRelevant()
    {
        var json = """
            {
              "relevant": false,
              "confidence": 1.0,
              "children": [],
              "requires_calendar_event": false,
              "requires_manual_review": false,
              "is_recurring": false
            }
            """;

        var result = JsonSerializer.Deserialize<AiAnalysisResult>(json);

        Assert.NotNull(result);
        Assert.False(result.Relevant);
        Assert.False(result.RequiresCalendarEvent);
        Assert.Empty(result.FamilyMembers);
    }

    [Fact]
    public void Deserialize_NullOptionalFields_DoesNotThrow()
    {
        var json = """
            {
              "relevant": true,
              "confidence": 0.75,
              "children": ["Tage"],
              "requires_calendar_event": true,
              "requires_manual_review": true,
              "is_recurring": false
            }
            """;

        var result = JsonSerializer.Deserialize<AiAnalysisResult>(json);

        Assert.NotNull(result);
        Assert.Null(result.Title);
        Assert.Null(result.Location);
        Assert.Null(result.Start);
        Assert.Null(result.End);
    }
}
