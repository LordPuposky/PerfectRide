using System.ComponentModel.DataAnnotations;

namespace PerfectRide.Data;

//Enum definition for available MaintenanceItem
public enum MaintenanceItem
{
    OilChange,
    TireRotation,
    BrakeInspection,
    BrakePadReplacement,
    AirFilterReplacement,
    CoolantService,
    TransmissionService,
    BatteryReplacement,
    SparkPlugReplacement,
    Other
}

// Data definition class for MaintenanceSchedule
public class MaintenanceSchedule
{
    public int Id { get; set; }
    [Required]
    public int VehicleId { get; set; }
    [Required]
    public MaintenanceItem MaintenanceItem { get; set; }
    [StringLength(500)]
    public string? Description { get; set; }
    [Range(1, 100000)]
    public int? IntervalMileage { get; set; }
    [Range(1, 24)]
    public int? IntervalMonths { get; set; }
    public Vehicle? Vehicle { get; set; }

    public ICollection<MaintenanceRecord> MaintenanceRecords { get; set; } = new List<MaintenanceRecord>();

}