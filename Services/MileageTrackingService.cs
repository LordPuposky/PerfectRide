using Microsoft.EntityFrameworkCore;
using PerfectRide.Data;

namespace PerfectRide.Services;


public class MileageTrackingService(ApplicationDbContext context)
{
    /* Determines if a Mileage tracking record exists for a given mileage.  Returns the record 
    if one exists.  Creates a new mileage record if no record exists.  Maintains an accurate 
    record of mileage allowing a many mileage sources to one mileage record relationship.*/
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

    //Retrieve MileageTracking records for a user across all vehicles. 
    public async Task<List<MileageTracking>> GetMileageTrackingsByUser(string userId)
    {
        return await context.MileageTrackings
        .AsNoTracking()
        .Where(t => t.Vehicle!.UserId == userId)
        .ToListAsync();
    }


    //Retrieve a vehicle's most current mileage reading.
    public async Task<int?> GetCurrentVehicleMileage(int vehicleId, string userId)
    {
        return await context.MileageTrackings
            .Where(m => m.VehicleId == vehicleId && m.Vehicle!.UserId == userId)
            .MaxAsync(m => (int?)m.Mileage);
    }


    //Retrieve the current mileage for every vehicle a user owns. 
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

    public async Task<(DateOnly, DateOnly)> GetDateRangeForVehicle(int vehicleId)
    {
        MileageTracking? beginningDateRecord = await context.MileageTrackings
        .Where(m => m.VehicleId == vehicleId)
        .OrderBy(m => m.Mileage)
        .FirstOrDefaultAsync();

        var beginningDate = beginningDateRecord?.DateRecorded ?? DateOnly.FromDateTime(DateTime.Now);

        MileageTracking? endingDateRecord = await context.MileageTrackings
        .Where(m => m.VehicleId == vehicleId)
        .OrderByDescending(m => m.Mileage)
        .FirstOrDefaultAsync();

        var endingDate = endingDateRecord?.DateRecorded ?? DateOnly.FromDateTime(DateTime.Now);

        return (beginningDate, endingDate);
    }

}