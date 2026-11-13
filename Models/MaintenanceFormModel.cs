using System.ComponentModel.DataAnnotations;
using PerfectRide.Data;

namespace PerfectRide.Models;

/// <summary>
/// Form data for adding or editing a maintenance record.
/// It is separate from the database entity so the form only exposes
/// the fields a user can change.
/// </summary>
public class MaintenanceFormModel
{
    [Required(ErrorMessage = "Please choose a service.")]
    public MaintenanceItem? MaintenanceItem { get; set; }

    [Required(ErrorMessage = "Please enter the service date.")]
    public DateOnly? DatePerformed { get; set; }

    [Required(ErrorMessage = "Please enter the mileage.")]
    [Range(0, 999999, ErrorMessage = "Mileage must be between 0 and 999,999.")]
    public int? Mileage { get; set; }

    [Range(typeof(decimal), "0", "1000000", ErrorMessage = "Cost must be between $0 and $1,000,000.")]
    public decimal? Cost { get; set; }

    [StringLength(100, ErrorMessage = "The service provider can have up to 100 characters.")]
    public string? ServiceProvider { get; set; }

    [StringLength(500, ErrorMessage = "Notes can have up to 500 characters.")]
    public string? Description { get; set; }

    /// <summary>
    /// Rules that involve more than one field.
    /// </summary>
    /// <summary>
    /// Rules that involve more than one field or today's date.
    /// The form shows each message under the field it belongs to.
    /// </summary>
    public IEnumerable<(string FieldName, string Message)> GetRuleErrors()
    {
        if (DatePerformed > DateOnly.FromDateTime(DateTime.Today))
        {
            yield return (nameof(DatePerformed), "The service date cannot be in the future.");
        }

        if (MaintenanceItem == Data.MaintenanceItem.Other && string.IsNullOrWhiteSpace(Description))
        {
            yield return (nameof(Description), "Please describe the service in the notes when you choose Other.");
        }
    }

    /// <summary>
    /// Creates a form model with the values of an existing record (used by the Edit page).
    /// </summary>
    public static MaintenanceFormModel FromRecord(MaintenanceRecord record) => new()
    {
        MaintenanceItem = record.MaintenanceItem,
        DatePerformed = record.DatePerformed,
        Mileage = record.MileageTracking?.Mileage,
        Cost = record.Cost,
        ServiceProvider = record.ServiceProvider,
        Description = record.Description
    };

    /// <summary>
    /// Copies the form values into a maintenance record. Call only after validation passed.
    /// </summary>
    public void ApplyTo(MaintenanceRecord record)
    {
        record.MaintenanceItem = MaintenanceItem!.Value;
        record.DatePerformed = DatePerformed!.Value;
        record.Cost = Cost;
        record.ServiceProvider = string.IsNullOrWhiteSpace(ServiceProvider) ? null : ServiceProvider.Trim();
        record.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
    }
}