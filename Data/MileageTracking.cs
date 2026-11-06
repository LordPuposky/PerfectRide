using System.ComponentModel.DataAnnotations;

namespace PerfectRide.Data;

public enum MileageSource
{
    Manual,
    FuelRecord,
    MaintenanceRecord
}

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
    public MaintenanceRecord? MaintenanceRecord { get; set; }
    public Vehicle? Vehicle { get; set; }
}