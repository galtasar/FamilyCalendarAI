using System.Text.Json;
using FamilyCalendar.Core.Models;

namespace FamilyCalendar.UnitTests;

public class AiAnalysisResultDeserializationTests
{
    [Fact]
    public void Deserialize_SingleEvent_MapsAllFields()
    {
        var json = """
            {
              "relevant": true,
              "confidence": 0.93,
              "summary": "Föräldramöte för Vera",
              "events": [
                {
                  "family_members": ["Vera"],
                  "event_type": "Föräldramöte",
                  "title": "Föräldramöte klass 5",
                  "start": "2026-09-14T18:00:00+02:00",
                  "end":   "2026-09-14T19:30:00+02:00",
                  "has_time": true,
                  "location": "Vattholmaskolan",
                  "requires_manual_review": false,
                  "is_recurring": false,
                  "summary": "Föräldramöte för Vera"
                }
              ]
            }
            """;

        var result = JsonSerializer.Deserialize<AiAnalysisResult>(json);

        Assert.NotNull(result);
        Assert.True(result.Relevant);
        Assert.Equal(0.93, result.Confidence);
        Assert.Single(result.Events);

        var evt = result.Events[0];
        Assert.Equal(["Vera"], evt.FamilyMembers);
        Assert.Equal("Föräldramöte klass 5", evt.Title);
        Assert.Equal("Vattholmaskolan", evt.Location);
        Assert.True(evt.HasTime);
        Assert.False(evt.RequiresManualReview);
        Assert.False(evt.IsRecurring);
        Assert.NotNull(evt.Start);
        Assert.NotNull(evt.End);
    }

    [Fact]
    public void Deserialize_MultipleEvents_MapsEachItem()
    {
        var json = """
            {
              "relevant": true,
              "confidence": 0.95,
              "events": [
                {
                  "family_members": ["Vera"],
                  "title": "Skansenutflykt",
                  "start": "2026-05-28T08:15:00+02:00",
                  "end":   "2026-05-28T16:30:00+02:00",
                  "has_time": true,
                  "requires_manual_review": false,
                  "is_recurring": false
                },
                {
                  "family_members": ["Tage"],
                  "title": "Friidrottsdag",
                  "start": "2026-05-26T08:10:00+02:00",
                  "end":   "2026-05-26T13:20:00+02:00",
                  "has_time": true,
                  "requires_manual_review": false,
                  "is_recurring": false
                }
              ]
            }
            """;

        var result = JsonSerializer.Deserialize<AiAnalysisResult>(json);

        Assert.NotNull(result);
        Assert.Equal(2, result.Events.Count);
        Assert.Equal("Skansenutflykt", result.Events[0].Title);
        Assert.Equal("Friidrottsdag", result.Events[1].Title);
    }

    [Fact]
    public void Deserialize_IrrelevantEmail_HasEmptyEvents()
    {
        var json = """
            {
              "relevant": false,
              "confidence": 1.0,
              "summary": null,
              "events": []
            }
            """;

        var result = JsonSerializer.Deserialize<AiAnalysisResult>(json);

        Assert.NotNull(result);
        Assert.False(result.Relevant);
        Assert.Empty(result.Events);
    }

    [Fact]
    public void Deserialize_NullOptionalFields_DoesNotThrow()
    {
        var json = """
            {
              "relevant": true,
              "confidence": 0.75,
              "events": [
                {
                  "family_members": ["Tage"],
                  "has_time": false,
                  "requires_manual_review": true,
                  "is_recurring": false
                }
              ]
            }
            """;

        var result = JsonSerializer.Deserialize<AiAnalysisResult>(json);

        Assert.NotNull(result);
        Assert.Single(result.Events);
        var evt = result.Events[0];
        Assert.Null(evt.Title);
        Assert.Null(evt.Location);
        Assert.Null(evt.Start);
        Assert.Null(evt.End);
    }
}
