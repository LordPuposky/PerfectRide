using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PerfectRide.Data;
using PerfectRide.Helpers;

namespace PerfectRide.Services;

/// <summary>
/// Handles the maintenance records of a user's vehicles.
/// Each record is linked to a MileageTracking reading. Readings can be shared:
/// several services from the same visit (same vehicle, date, and mileage) use one reading.
/// Readings are obtained through MileageTrackingService, and a reading is deleted
/// only when no other record uses it.
/// </summary>
public class MaintenanceService(ApplicationDbContext context, MileageTrackingService mileageTrackingService)
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
    /// Returns one Maintenance Schedules for one vehicle. 
    /// Returns an empty list if the vehicle has no maintenance schedules.
    /// </summary>
    public async Task<List<MaintenanceSchedule>> GetMaintenanceSchedulesForVehicleAsync(int vehicleId, string userId)
    {
        return await context.MaintenanceSchedules
        .AsNoTracking()
        .Where(s => s.VehicleId == vehicleId && s.Vehicle!.UserId == userId)
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
    /// Adds a maintenance record and links it to a mileage reading
    /// (an existing one from the same visit, or a new one), in one transaction.
    /// </summary>
    public async Task<int> AddRecordAsync(MaintenanceRecord record, int mileage, string userId)
    {
        await ValidateAsync(record, mileage, userId, excludedReadingId: null);

        await using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            record.MileageTracking = await GetOrCreateReadingAsync(record.VehicleId, record.DatePerformed, mileage);
            // Link the record to the vehicle's schedule for this service, if there is one.
            // "Other" keeps the schedule the user chose in the form (it can be none).
            if (record.MaintenanceItem != MaintenanceItem.Other)
            {
                record.MaintenanceScheduleId = await FindScheduleIdAsync(record.VehicleId, record.MaintenanceItem);
            }
            context.MaintenanceRecords.Add(record);

            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            // Blazor Server keeps the same DbContext for the whole session.
            // Clear the failed changes so they are not saved again on the next attempt.
            context.ChangeTracker.Clear();
            throw;
        }
        return record.Id;
    }

    /// <summary>
    /// Updates a maintenance record. If the date or mileage changes, the record is linked
    /// to another reading, because the current one may be shared with other records.
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
        // "Other" keeps the schedule the user chose in the form (it can be none).
        // For the other service types, keep the link if the type is the same.
        // Otherwise (or if the record had no link), use the vehicle's schedule for this service type.
        if (updated.MaintenanceItem != MaintenanceItem.Other)
        {
            updated.MaintenanceScheduleId = updated.MaintenanceItem == existing.MaintenanceItem && existing.MaintenanceScheduleId is not null
                ? existing.MaintenanceScheduleId
                : await FindScheduleIdAsync(existing.VehicleId, updated.MaintenanceItem);
        }

        var oldReading = existing.MileageTracking!;
        bool readingChanged = oldReading.Mileage != mileage || oldReading.DateRecorded != updated.DatePerformed;
        bool oldReadingShared = await IsReadingUsedByOthersAsync(oldReading.Id, existing.Id);

        // The old reading is left out of the odometer check only when it will be replaced
        // (the date or mileage changed) and no other record uses it. Otherwise it is still checked.
        bool excludeOldReading = readingChanged && !oldReadingShared;
        await ValidateAsync(updated, mileage, userId, excludedReadingId: excludeOldReading ? oldReading.Id : null);

        await using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            existing.MaintenanceItem = updated.MaintenanceItem;
            existing.Description = updated.Description;
            existing.DatePerformed = updated.DatePerformed;
            existing.Cost = updated.Cost;
            existing.ServiceProvider = updated.ServiceProvider;
            existing.MaintenanceScheduleId = updated.MaintenanceScheduleId;

            if (readingChanged)
            {
                existing.MileageTracking = await GetOrCreateReadingAsync(existing.VehicleId, updated.DatePerformed, mileage);
                RemoveReadingIfUnused(oldReading, oldReadingShared);
            }

            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            // Same reason as in AddRecordAsync: do not keep failed changes in the session.
            context.ChangeTracker.Clear();
            throw;
        }
        return true;
    }

    /// <summary>
    /// Deletes a maintenance record, and its reading if no other record uses it,
    /// in one transaction. Returns false if the record does not exist or belongs to another user.
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

        var reading = record.MileageTracking!;
        bool readingShared = await IsReadingUsedByOthersAsync(reading.Id, record.Id);

        await using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            context.MaintenanceRecords.Remove(record);
            RemoveReadingIfUnused(reading, readingShared);

            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            context.ChangeTracker.Clear();
            throw;
        }
        return true;
    }

    /// <summary>
    /// Returns the id of the vehicle's schedule for this service type, or null if there is none.
    /// "Other" is not linked automatically; the user chooses its schedule in the form.
    /// </summary>
    private async Task<int?> FindScheduleIdAsync(int vehicleId, MaintenanceItem item)
    {
        if (item == MaintenanceItem.Other)
        {
            return null;
        }

        return await context.MaintenanceSchedules
            .Where(s => s.VehicleId == vehicleId && s.MaintenanceItem == item)
            .Select(s => (int?)s.Id)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Gets the reading for this vehicle, date, and mileage from MileageTrackingService.
    /// A new reading is not tracked yet, so it is added to the context, as the service guide requires.
    /// </summary>
    private async Task<MileageTracking> GetOrCreateReadingAsync(int vehicleId, DateOnly date, int mileage)
    {
        var reading = await mileageTrackingService.AssignMileageTrackingRecord(
            vehicleId, date, mileage, MileageSource.MaintenanceRecord);

        if (context.Entry(reading).State == EntityState.Detached)
        {
            context.MileageTrackings.Add(reading);
        }
        return reading;
    }

    /// <summary>
    /// Returns true if another maintenance record or a fuel record uses this reading.
    /// </summary>
    private async Task<bool> IsReadingUsedByOthersAsync(int readingId, int currentRecordId)
    {
        return await context.MaintenanceRecords.AnyAsync(r => r.MileageTrackingId == readingId && r.Id != currentRecordId)
            || await context.FuelRecords.AnyAsync(f => f.MileageTrackingId == readingId);
    }

    /// <summary>
    /// Deletes a reading that no other record uses. Readings entered manually or by
    /// the fuel module are never deleted here.
    /// </summary>
    private void RemoveReadingIfUnused(MileageTracking reading, bool isShared)
    {
        if (!isShared && reading.Source == MileageSource.MaintenanceRecord)
        {
            context.MileageTrackings.Remove(reading);
        }
    }

    /// <summary>
    /// Applies the business rules from the database guide.
    /// Throws an exception with a user-friendly message if a rule fails.
    /// </summary>

    private async Task ValidateAsync(
        MaintenanceRecord record,
        int mileage,
        string userId,
        int? excludedReadingId)
    {
        bool ownsVehicle = await context.Vehicles
            .AnyAsync(v => v.Id == record.VehicleId && v.UserId == userId);

        if (!ownsVehicle)
        {
            throw new UnauthorizedAccessException(
                "The vehicle does not belong to the current user.");
        }

        // Validate mileage against the vehicle's existing mileage history.
        await mileageTrackingService.ValidateMileageAsync(
            record.VehicleId,
            record.DatePerformed,
            mileage,
            excludedReadingId);

        if (record.MaintenanceItem == MaintenanceItem.Other &&
            string.IsNullOrWhiteSpace(record.Description))
        {
            throw new ArgumentException(
                "A description is required when the maintenance item is Other.");
        }

        // The same service can't be logged twice for the same vehicle on the same date.
        // "Other" is allowed more than once, because its description tells the services apart.
        // When editing, the record itself is ignored.
        if (record.MaintenanceItem != MaintenanceItem.Other)
        {
            bool isDuplicate = await context.MaintenanceRecords.AnyAsync(r =>
                r.VehicleId == record.VehicleId &&
                r.DatePerformed == record.DatePerformed &&
                r.MaintenanceItem == record.MaintenanceItem &&
                r.Id != record.Id);

            if (isDuplicate)
            {
                var serviceName = DisplayNameHelper.ToDisplayName(record.MaintenanceItem);
                var usCulture = CultureInfo.GetCultureInfo("en-US");
                var date = record.DatePerformed.ToString("MMM d, yyyy", usCulture);

                throw new ArgumentException(
                    $"{serviceName} is already recorded for this vehicle on {date}. Please edit that record instead.");
            }
        }

        // The schedule must belong to the same vehicle and be for the same service type.
        if (record.MaintenanceScheduleId is int scheduleId)
        {
            bool scheduleMatches = await context.MaintenanceSchedules
                .AnyAsync(s => s.Id == scheduleId
                    && s.VehicleId == record.VehicleId
                    && s.MaintenanceItem == record.MaintenanceItem);

            if (!scheduleMatches)
            {
                throw new ArgumentException(
                    "The maintenance schedule must belong to the same vehicle and service type.");
            }
        }
    }
}