using System.ComponentModel.DataAnnotations;

namespace PerfectRide.Data;

public class MaintenanceRecord
{
    public int Id { get; set; }
    public int VehicleId { get; set; }
    [Required]
    public MaintenanceItem MaintenanceItem { get; set; }
    [StringLength(500)]
    public string? Description { get; set; }
    public DateOnly DatePerformed { get; set; }
    [Range(0, int.MaxValue)]
    public decimal? Cost { get; set; }
    [StringLength(100)]
    public string? ServiceProvider { get; set; }
    public Vehicle? Vehicle { get; set; }
    public MaintenanceSchedule? MaintenanceSchedule { get; set; }
    public int? MaintenanceScheduleId { get; set; }
    public MileageTracking? MileageTracking { get; set; }
    public int MileageTrackingId { get; set; }

}