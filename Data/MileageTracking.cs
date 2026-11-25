using System.ComponentModel.DataAnnotations;

namespace PerfectRide.Data;

// Enum definition for acceptable MileageSource
public enum MileageSource
{
    Manual,
    FuelRecord,
    MaintenanceRecord
}

// Data definition class for MileageTracking
public class MileageTracking
{
    public int Id { get; set; }
    [Required]
    public int VehicleId { get; set; }
    [Required]
    public DateOnly DateRecorded { get; set; }
    [Required, Range(0, 999999)]
    public int Mileage { get; set; }
    [Required]
    public MileageSource Source { get; set; }
    public FuelRecord? FuelRecord { get; set; }
    public ICollection<MaintenanceRecord>? MaintenanceRecords { get; set; } = new List<MaintenanceRecord>();
    public Vehicle? Vehicle { get; set; }
}