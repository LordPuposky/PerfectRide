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

    public async Task<List<MileageTracking>> GetMileageTrackingsByUser(string userId)
    {
        return await context.MileageTrackings
        .AsNoTracking()
        .Where(t => t.Vehicle!.UserId == userId)
        .ToListAsync();
    }

    public async Task<int?> GetCurrentVehicleMileage(int vehicleId, string userId)
    {
        return await context.MileageTrackings
            .Where(m => m.VehicleId == vehicleId && m.Vehicle!.UserId == userId)
            .MaxAsync(m => (int?)m.Mileage);
    }

    public async Task<Dictionary<int, int>> GetCurrentMileageByUser(string userId)
    {
        return await context.MileageTrackings
            .Where(m => m.Vehicle!.UserId == userId)
            .GroupBy(m => m.VehicleId)
            .Select(g => new
            {
                VehicleId = g.Key,
                Mileage = g.Max(m => m.Mileage)
            })
            .ToDictionaryAsync(x => x.VehicleId, x => x.Mileage);
    }
}