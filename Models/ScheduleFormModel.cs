using System.ComponentModel.DataAnnotations;
using PerfectRide.Data;

namespace PerfectRide.Models;

/// <summary>
/// Form data for adding or editing a maintenance schedule.
/// It is separate from the database entity so the form only exposes
/// the fields a user can change.
/// </summary>
public class ScheduleFormModel
{
    [Required(ErrorMessage = "Please choose a service.")]
    public MaintenanceItem? MaintenanceItem { get; set; }

    [Range(1, 100000, ErrorMessage = "The mileage interval must be between 1 and 100,000 miles.")]
    public int? IntervalMileage { get; set; }

    [Range(1, 24, ErrorMessage = "The time interval must be between 1 and 24 months.")]
    public int? IntervalMonths { get; set; }

    [StringLength(500, ErrorMessage = "Notes can have up to 500 characters.")]
    public string? Description { get; set; }

    /// <summary>
    /// Rules that involve more than one field.
    /// The form shows each message under the field it belongs to.
    /// </summary>
    public IEnumerable<(string FieldName, string Message)> GetRuleErrors()
    {
        if (IntervalMileage is null && IntervalMonths is null)
        {
            yield return (nameof(IntervalMileage), "Enter a mileage interval, a time interval, or both.");
        }

        if (MaintenanceItem == Data.MaintenanceItem.Other && string.IsNullOrWhiteSpace(Description))
        {
            yield return (nameof(Description), "Please describe the service in the notes when you choose Other.");
        }
    }

    /// <summary>
    /// Creates a form model with the values of an existing schedule (used by the Edit page).
    /// </summary>
    public static ScheduleFormModel FromSchedule(MaintenanceSchedule schedule) => new()
    {
        MaintenanceItem = schedule.MaintenanceItem,
        IntervalMileage = schedule.IntervalMileage,
        IntervalMonths = schedule.IntervalMonths,
        Description = schedule.Description
    };

    /// <summary>
    /// Copies the form values into a schedule. Call only after validation passed.
    /// </summary>
    public void ApplyTo(MaintenanceSchedule schedule)
    {
        schedule.MaintenanceItem = MaintenanceItem!.Value;
        schedule.IntervalMileage = IntervalMileage;
        schedule.IntervalMonths = IntervalMonths;
        schedule.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
    }
}