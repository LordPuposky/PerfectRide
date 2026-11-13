using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PerfectRide.Data;

namespace PerfectRide.Services;

/// <summary>
/// Handles the maintenance records of a user's vehicles.
/// Every maintenance record has exactly one MileageTracking entry,
/// so both are created, updated, and deleted together.
/// </summary>
public class MaintenanceService(ApplicationDbContext context)
{
    /// <summary>
    /// Returns the maintenance history of one vehicle, newest first.
    /// Returns an empty list if the vehicle does not belong to the user.
    /// </summary>
    public async Task<List<MaintenanceRecord>> GetRecordsForVehicleAsync(int vehicleId, string userId)
    {
        return await context.MaintenanceRecords
            .AsNoTracking()
            .Where(r => r.VehicleId == vehicleId && r.Vehicle!.UserId == userId)
            .Include(r => r.MileageTracking)
            .Include(r => r.MaintenanceSchedule)
            .OrderByDescending(r => r.DatePerformed)
            .ThenByDescending(r => r.MileageTracking!.Mileage)
            .ToListAsync();
    }

    /// <summary>
    /// Returns one maintenance record, or null if it does not exist
    /// or belongs to another user.
    /// </summary>
    public async Task<MaintenanceRecord?> GetRecordAsync(int recordId, string userId)
    {
        return await context.MaintenanceRecords
            .AsNoTracking()
            .Include(r => r.Vehicle)
            .Include(r => r.MileageTracking)
            .FirstOrDefaultAsync(r => r.Id == recordId && r.Vehicle!.UserId == userId);
    }

    /// <summary>
    /// Returns the vehicle if it belongs to the user; otherwise null.
    /// </summary>
    public async Task<Vehicle?> GetVehicleAsync(int vehicleId, string userId)
    {
        return await context.Vehicles
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == vehicleId && v.UserId == userId);
    }

    /// <summary>
    /// Returns the highest mileage recorded for the vehicle, or null if there are no readings.
    /// </summary>
    public async Task<int?> GetLatestMileageAsync(int vehicleId, string userId)
    {
        return await context.MileageTrackings
            .Where(m => m.VehicleId == vehicleId && m.Vehicle!.UserId == userId)
            .MaxAsync(m => (int?)m.Mileage);
    }

    /// <summary>
    /// Adds a maintenance record and its MileageTracking entry.
    /// Both are saved in one SaveChanges call, which runs in a single transaction.
    /// </summary>
    public async Task<int> AddRecordAsync(MaintenanceRecord record, int mileage, string userId)
    {
        await ValidateAsync(record, mileage, userId);

        record.MileageTracking = new MileageTracking
        {
            VehicleId = record.VehicleId,
            DateRecorded = record.DatePerformed,
            Mileage = mileage,
            Source = MileageSource.MaintenanceRecord
        };

        context.MaintenanceRecords.Add(record);
        try
        {
            await context.SaveChangesAsync();
        }
        catch
        {
            // Blazor Server keeps the same DbContext for the whole session.
            // Clear the failed record so it is not saved again on the next attempt.
            context.ChangeTracker.Clear();
            throw;
        }
        return record.Id;
    }

    /// <summary>
    /// Updates a maintenance record and keeps its MileageTracking entry in sync.
    /// Returns false if the record does not exist or belongs to another user.
    /// </summary>
    public async Task<bool> UpdateRecordAsync(MaintenanceRecord updated, int mileage, string userId)
    {
        var existing = await context.MaintenanceRecords
            .Include(r => r.MileageTracking)
            .FirstOrDefaultAsync(r => r.Id == updated.Id && r.Vehicle!.UserId == userId);

        if (existing is null)
        {
            return false;
        }

        // The vehicle of a record cannot be changed.
        updated.VehicleId = existing.VehicleId;
        await ValidateAsync(updated, mileage, userId);

        existing.MaintenanceItem = updated.MaintenanceItem;
        existing.Description = updated.Description;
        existing.DatePerformed = updated.DatePerformed;
        existing.Cost = updated.Cost;
        existing.ServiceProvider = updated.ServiceProvider;
        existing.MaintenanceScheduleId = updated.MaintenanceScheduleId;

        existing.MileageTracking!.Mileage = mileage;
        existing.MileageTracking.DateRecorded = updated.DatePerformed;

        try
        {
            await context.SaveChangesAsync();
        }
        catch
        {
            // Same reason as in AddRecordAsync: do not keep failed changes in the session.
            context.ChangeTracker.Clear();
            throw;
        }
        return true;
    }

    /// <summary>
    /// Deletes a maintenance record and its MileageTracking entry in the same transaction.
    /// Returns false if the record does not exist or belongs to another user.
    /// </summary>
    public async Task<bool> DeleteRecordAsync(int recordId, string userId)
    {
        var record = await context.MaintenanceRecords
            .Include(r => r.MileageTracking)
            .FirstOrDefaultAsync(r => r.Id == recordId && r.Vehicle!.UserId == userId);

        if (record is null)
        {
            return false;
        }

        context.MaintenanceRecords.Remove(record);
        if (record.MileageTracking is not null)
        {
            context.MileageTrackings.Remove(record.MileageTracking);
        }

        await context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Applies the business rules from the database guide.
    /// Throws an exception with a user-friendly message if a rule fails.
    /// </summary>
    private async Task ValidateAsync(MaintenanceRecord record, int mileage, string userId)
    {
        bool ownsVehicle = await context.Vehicles
            .AnyAsync(v => v.Id == record.VehicleId && v.UserId == userId);
        if (!ownsVehicle)
        {
            throw new UnauthorizedAccessException("The vehicle does not belong to the current user.");
        }

        if (mileage < 0 || mileage > 999999)
        {
            throw new ArgumentException("Mileage must be between 0 and 999,999.");
        }

        // An odometer never goes backward: the mileage must fit between
        // the readings recorded before and after this date for the same vehicle.
        // When editing, the record's own reading is ignored.
        var otherReadings = context.MileageTrackings
            .Where(m => m.VehicleId == record.VehicleId)
            .Where(m => m.MaintenanceRecord == null || m.MaintenanceRecord.Id != record.Id);

        var usCulture = CultureInfo.GetCultureInfo("en-US");
        var date = record.DatePerformed.ToString("MMM d, yyyy", usCulture);

        int? highestBefore = await otherReadings
            .Where(m => m.DateRecorded < record.DatePerformed)
            .MaxAsync(m => (int?)m.Mileage);
        if (highestBefore is int before && mileage < before)
        {
            throw new ArgumentException(
                $"The mileage cannot be lower than {before.ToString("N0", usCulture)} mi, which was recorded before {date}.");
        }

        int? lowestAfter = await otherReadings
            .Where(m => m.DateRecorded > record.DatePerformed)
            .MinAsync(m => (int?)m.Mileage);
        if (lowestAfter is int after && mileage > after)
        {
            throw new ArgumentException(
                $"The mileage cannot be higher than {after.ToString("N0", usCulture)} mi, which was recorded after {date}.");
        }

        if (record.MaintenanceItem == MaintenanceItem.Other && string.IsNullOrWhiteSpace(record.Description))
        {
            throw new ArgumentException("A description is required when the maintenance item is Other.");
        }

        if (record.MaintenanceScheduleId is int scheduleId)
        {
            bool sameVehicle = await context.MaintenanceSchedules
                .AnyAsync(s => s.Id == scheduleId && s.VehicleId == record.VehicleId);
            if (!sameVehicle)
            {
                throw new ArgumentException("The maintenance schedule must belong to the same vehicle.");
            }
        }
    }
}