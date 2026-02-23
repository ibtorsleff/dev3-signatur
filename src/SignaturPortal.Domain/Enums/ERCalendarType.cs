namespace SignaturPortal.Domain.Enums;

/// <summary>
/// Mirrors legacy ERCalendarType enum (GenericObjects.cs).
/// Values are stored as CalendarTypeId on ERActivity.
/// </summary>
public enum ERCalendarType
{
    NoCalendarFunction = 0,
    OpenCalendar = 1,
    ClosedCalendar = 2
}
