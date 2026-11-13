using System.ComponentModel.DataAnnotations;

namespace PerfectRide.Data;

public class Vehicle
{
    public int Id { get; set; }

    [Required, Range(1900, 2300)]
    public int? Year { get; set; }
    [StringLength(30), Required]
    public string Make { get; set; } = string.Empty;
    [StringLength(30), Required]
    public string Model { get; set; } = string.Empty;
    [StringLength(17)]
    public string? VIN { get; set; }
    [StringLength(20)]
    public string? Plate { get; set; }
    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }
    public ICollection<MaintenanceSchedule> MaintenanceSchedules { get; set; } = new List<MaintenanceSchedule>();
    public ICollection<MaintenanceRecord> MaintenanceRecords { get; set; } = new List<MaintenanceRecord>();
    public ICollection<FuelRecord> FuelRecords { get; set; } = new List<FuelRecord>();
    public ICollection<Document> Documents { get; set; } = new List<Document>();
    public ICollection<MileageTracking> MileageTrackings { get; set; } = new List<MileageTracking>();
}