using Microsoft.EntityFrameworkCore;
using PerfectRide.Data;

namespace PerfectRide.Services;

public class MileageTrackingService(ApplicationDbContext context)
{
    public async Task<MileageTracking> AssignMileageTrackingRecord(int vehicleId, DateOnly trackingDate, int trackingMileage, MileageSource source)
    {
        MileageTracking? existingRecord = await context.MileageTrackings
        .FirstOrDefaultAsync(m => m.VehicleId == vehicleId
        && m.DateRecorded == trackingDate
        && m.Mileage == trackingMileage);

        if (existingRecord != null)
        {
            return existingRecord;
        }
        else
        {
            MileageTracking newMileageTracking = new MileageTracking
            {
                VehicleId = vehicleId,
                DateRecorded = trackingDate,
                Mileage = trackingMileage,
                Source = source
            };

            return newMileageTracking;

        }
    }
}