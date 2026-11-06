using System.ComponentModel.DataAnnotations;

namespace PerfectRide.Data;

public class FuelRecord
{
    public int Id { get; set; }
    public int VehicleId { get; set; }
    public DateOnly DateFilled { get; set; }
    [Range(0.01, 100)]
    public decimal Gallons { get; set; }
    [Range(0.01, 50)]
    public decimal PricePerGallon { get; set; }
    public decimal TotalCost { get; set; }
    public Vehicle? Vehicle { get; set; }
    public int MileageTrackingId { get; set; }
    public bool IsFullTank { get; set; } = false;
    public MileageTracking? MileageTracking { get; set; }

}