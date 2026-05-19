using FamilyCalendar.Core.Models;

namespace FamilyCalendar.Calendar.Services;

public interface IGoogleCalendarService
{
    Task<string> CreateEventAsync(CalendarEvent calendarEvent);
    Task UpdateEventAsync(CalendarEvent calendarEvent);
    Task DeleteEventAsync(CalendarEvent calendarEvent);
}
