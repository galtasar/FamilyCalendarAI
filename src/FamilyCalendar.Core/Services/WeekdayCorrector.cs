using FamilyCalendar.Core.Models;

namespace FamilyCalendar.Core.Services;

/// <summary>
/// LLMs are unreliable at mapping a weekday ("på torsdag") to a calendar date and often land
/// one day off. When the AI reports which weekday the email mentions, trust the weekday and
/// move the date to the nearest matching day.
/// </summary>
public static class WeekdayCorrector
{
    private static readonly Dictionary<string, DayOfWeek> WeekdayNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["måndag"] = DayOfWeek.Monday,
        ["tisdag"] = DayOfWeek.Tuesday,
        ["onsdag"] = DayOfWeek.Wednesday,
        ["torsdag"] = DayOfWeek.Thursday,
        ["fredag"] = DayOfWeek.Friday,
        ["lördag"] = DayOfWeek.Saturday,
        ["söndag"] = DayOfWeek.Sunday,
        ["monday"] = DayOfWeek.Monday,
        ["tuesday"] = DayOfWeek.Tuesday,
        ["wednesday"] = DayOfWeek.Wednesday,
        ["thursday"] = DayOfWeek.Thursday,
        ["friday"] = DayOfWeek.Friday,
        ["saturday"] = DayOfWeek.Saturday,
        ["sunday"] = DayOfWeek.Sunday,
    };

    /// <summary>
    /// Shifts Start/End so Start falls on the weekday named in <see cref="AiEventDraft.Weekday"/>.
    /// Returns true if the draft was changed.
    /// </summary>
    public static bool Correct(AiEventDraft draft)
    {
        if (draft.Start is not { } start || string.IsNullOrWhiteSpace(draft.Weekday))
            return false;

        var name = draft.Weekday.Trim().TrimEnd('s'); // "torsdags" → "torsdag"
        if (!WeekdayNames.TryGetValue(name, out var expected) && !WeekdayNames.TryGetValue(draft.Weekday.Trim(), out expected))
            return false;

        // DayOfWeek in the offset the AI supplied (Europe/Stockholm), i.e. the local calendar day.
        var diff = ((int)expected - (int)start.DayOfWeek + 7) % 7;
        if (diff == 0)
            return false;
        if (diff > 3)
            diff -= 7; // pick the nearest matching day: -3..+3

        draft.Start = start.AddDays(diff);
        draft.End = draft.End?.AddDays(diff);
        draft.RequiresManualReview = true;
        return true;
    }
}
