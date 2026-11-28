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
/// When IsEstimated is true, no service is recorded yet: LastDate and LastMileage are the
/// vehicle's first mileage reading (the start of tracking), and the next due is an estimate.
/// Any value can be null when there is not enough information.
/// </summary>
public record ScheduleDueInfo(
    DateOnly? LastDate,
    int? LastMileage,
    DateOnly? NextDate,
    int? NextMileage,
    ScheduleStatus Status,
    bool IsEstimated = false);

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
    /// If no service is recorded yet, the next due is estimated from firstReading
    /// (the vehicle's first mileage reading), the same idea the dashboard uses.
    /// "today" is a parameter so the calculation can be tested with any date.
    /// </summary>
    public static ScheduleDueInfo GetDueInfo(
        MaintenanceSchedule schedule, int? currentMileage, DateOnly today, MileageTracking? firstReading = null)
    {
        var lastRecord = schedule.MaintenanceRecords
            .OrderByDescending(r => r.DatePerformed)
            .ThenByDescending(r => r.MileageTracking?.Mileage ?? 0)
            .FirstOrDefault();

        DateOnly baseDate;
        int? lastMileage;
        bool isEstimated = false;

        if (lastRecord is not null)
        {
            baseDate = lastRecord.DatePerformed;
            lastMileage = lastRecord.MileageTracking?.Mileage;
        }
        else if (firstReading is not null)
        {
            // No service yet: count the interval from the start of tracking.
            baseDate = firstReading.DateRecorded;
            lastMileage = firstReading.Mileage;
            isEstimated = true;
        }
        else
        {
            // No service and no mileage readings: nothing to calculate.
            return new ScheduleDueInfo(null, null, null, null, ScheduleStatus.NoServiceYet);
        }

        DateOnly? nextDate = schedule.IntervalMonths is int months
            ? baseDate.AddMonths(months)
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

        return new ScheduleDueInfo(baseDate, lastMileage, nextDate, nextMileage, status, isEstimated);
    }
}