using PerfectRide.Data;

namespace PerfectRide.Helpers;

/// <summary>
/// The state of a maintenance schedule, shown as text on the page
/// (not only as a color) so every user can understand it.
/// </summary>
public enum ScheduleStatus
{
    NoServiceYet,
    Ok,
    DueSoon,
    Overdue
}

/// <summary>
/// The last service of a schedule, when it is due next, and its status.
/// Any value can be null when there is not enough information.
/// </summary>
public record ScheduleDueInfo(
    DateOnly? LastDate,
    int? LastMileage,
    DateOnly? NextDate,
    int? NextMileage,
    ScheduleStatus Status);

/// <summary>
/// Calculates when a scheduled service is due, based on its most recent linked record.
/// </summary>
public static class ScheduleStatusHelper
{
    // Same limits the dashboard uses for "upcoming" services.
    public const int DueSoonDays = 30;
    public const int DueSoonMiles = 500;

    /// <summary>
    /// Returns the due information of a schedule.
    /// The schedule must include its MaintenanceRecords with their MileageTracking.
    /// "today" is a parameter so the calculation can be tested with any date.
    /// </summary>
    public static ScheduleDueInfo GetDueInfo(MaintenanceSchedule schedule, int? currentMileage, DateOnly today)
    {
        var lastRecord = schedule.MaintenanceRecords
            .OrderByDescending(r => r.DatePerformed)
            .ThenByDescending(r => r.MileageTracking?.Mileage ?? 0)
            .FirstOrDefault();

        if (lastRecord is null)
        {
            return new ScheduleDueInfo(null, null, null, null, ScheduleStatus.NoServiceYet);
        }

        int? lastMileage = lastRecord.MileageTracking?.Mileage;

        DateOnly? nextDate = schedule.IntervalMonths is int months
            ? lastRecord.DatePerformed.AddMonths(months)
            : null;

        int? nextMileage = schedule.IntervalMileage is int interval && lastMileage is int last
            ? last + interval
            : null;

        bool overdueByDate = nextDate is DateOnly dueDate && today > dueDate;
        bool overdueByMileage = nextMileage is int dueMileage && currentMileage is int current && current > dueMileage;

        bool soonByDate = nextDate is DateOnly soonDate && soonDate.DayNumber - today.DayNumber <= DueSoonDays;
        bool soonByMileage = nextMileage is int soonMileage && currentMileage is int now && soonMileage - now <= DueSoonMiles;

        // An overdue service is only "Overdue", never "Due soon" as well.
        var status = overdueByDate || overdueByMileage ? ScheduleStatus.Overdue
            : soonByDate || soonByMileage ? ScheduleStatus.DueSoon
            : ScheduleStatus.Ok;

        return new ScheduleDueInfo(lastRecord.DatePerformed, lastMileage, nextDate, nextMileage, status);
    }
}