using Microsoft.EntityFrameworkCore;
using PerfectRide.Data;
using System.Globalization;

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



    public async Task ValidateMileageAsync(
        int vehicleId,
        DateOnly datePerformed,
        int mileage,
        int? excludedReadingId)
    {
        string high = "";
        string low = "";
        if (mileage < 0 || mileage > 999999)
        {
            throw new ArgumentException(
                "Mileage must be between 0 and 999,999.");
        }

        var otherReadings = context.MileageTrackings
            .Where(m => m.VehicleId == vehicleId);

        if (excludedReadingId is int readingId)
        {
            otherReadings = otherReadings
                .Where(m => m.Id != readingId);
        }

        var usCulture = CultureInfo.GetCultureInfo("en-US");
        var date = datePerformed.ToString("MMM d, yyyy", usCulture);

        int? highestBefore = await otherReadings
            .Where(m => m.DateRecorded < datePerformed)
            .MaxAsync(m => (int?)m.Mileage);

        int? lowestAfter = await otherReadings
            .Where(m => m.DateRecorded > datePerformed)
            .MinAsync(m => (int?)m.Mileage);

        if (highestBefore is int)
        {
            low = highestBefore.Value.ToString("N0", usCulture);
        }
        else
        {
            low = "0";
        }

        if (lowestAfter is int)
        {
            high = lowestAfter.Value.ToString("N0", usCulture);
        }
        else
        {
            high = "999,999";
        }

        if (highestBefore is int before && mileage < before)
        {
            throw new ArgumentException(
                $"The mileage must be between {low} and {high} mi for the date {date}.");
        }

        if (lowestAfter is int after && mileage > after)
        {
            throw new ArgumentException(
                $"The mileage must be between {low} and {high} mi for the date {date}.");
        }
    }


}