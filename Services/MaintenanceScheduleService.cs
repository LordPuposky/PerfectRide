using Microsoft.EntityFrameworkCore;
using PerfectRide.Data;
using PerfectRide.Helpers;

namespace PerfectRide.Services;

/// <summary>
/// Handles the maintenance schedules of a user's vehicles.
/// A schedule says how often a service should be done (every X miles, every Y months, or both).
/// Maintenance records of the same service are linked to the schedule, so the app can
/// calculate when the service is due next.
/// </summary>
public class MaintenanceScheduleService(ApplicationDbContext context)
{
    /// <summary>
    /// Returns all the maintenance schedules of the user's vehicles (used by the dashboard).
    /// </summary>
    public async Task<List<MaintenanceSchedule>> GetMaintenanceScheduleForUser(string userId)
    {
        return await context.MaintenanceSchedules
        .AsNoTracking()
        .Where(r => r.Vehicle!.UserId == userId)
        .ToListAsync();
    }

    /// <summary>
    /// Returns the schedules of one vehicle, with their linked maintenance records
    /// and mileage readings, so the page can show the last service and the next due.
    /// Returns an empty list if the vehicle does not belong to the user.
    /// </summary>
    public async Task<List<MaintenanceSchedule>> GetSchedulesForVehicleAsync(int vehicleId, string userId)
    {
        return await context.MaintenanceSchedules
            .AsNoTracking()
            .Where(s => s.VehicleId == vehicleId && s.Vehicle!.UserId == userId)
            .Include(s => s.MaintenanceRecords)
                .ThenInclude(r => r.MileageTracking)
            .OrderBy(s => s.MaintenanceItem)
            .ThenBy(s => s.Description)
            .ToListAsync();
    }

    /// <summary>
    /// Returns one schedule, or null if it does not exist or belongs to another user.
    /// </summary>
    public async Task<MaintenanceSchedule?> GetScheduleAsync(int scheduleId, string userId)
    {
        return await context.MaintenanceSchedules
            .AsNoTracking()
            .Include(s => s.Vehicle)
            .FirstOrDefaultAsync(s => s.Id == scheduleId && s.Vehicle!.UserId == userId);
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
    public async Task<int?> GetCurrentMileageAsync(int vehicleId, string userId)
    {
        return await context.MileageTrackings
            .Where(m => m.VehicleId == vehicleId && m.Vehicle!.UserId == userId)
            .MaxAsync(m => (int?)m.Mileage);
    }

    /// <summary>
    /// Returns how many schedules each of the user's vehicles has (vehicle id, count).
    /// Vehicles without schedules are not in the dictionary.
    /// </summary>
    public async Task<Dictionary<int, int>> GetScheduleCountsAsync(string userId)
    {
        return await context.MaintenanceSchedules
            .Where(s => s.Vehicle!.UserId == userId)
            .GroupBy(s => s.VehicleId)
            .Select(g => new { VehicleId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.VehicleId, x => x.Count);
    }

    /// <summary>
    /// Adds a schedule and links the vehicle's earlier records of the same service to it,
    /// so the next due is calculated from the real history. Both happen in one transaction.
    /// </summary>
    public async Task<int> AddScheduleAsync(MaintenanceSchedule schedule, string userId)
    {
        await ValidateAsync(schedule, userId);

        await using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            context.MaintenanceSchedules.Add(schedule);
            await context.SaveChangesAsync();

            await LinkUnscheduledRecordsAsync(schedule);
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
        return schedule.Id;
    }

    /// <summary>
    /// Updates a schedule. If the service type changes, the old records are unlinked.
    /// Earlier records of the schedule's service type that have no schedule are linked to it.
    /// Returns false if the schedule does not exist or belongs to another user.
    /// </summary>
    public async Task<bool> UpdateScheduleAsync(MaintenanceSchedule updated, string userId)
    {
        var existing = await context.MaintenanceSchedules
            .FirstOrDefaultAsync(s => s.Id == updated.Id && s.Vehicle!.UserId == userId);

        if (existing is null)
        {
            return false;
        }

        // The vehicle of a schedule cannot be changed.
        updated.VehicleId = existing.VehicleId;
        await ValidateAsync(updated, userId);

        bool serviceChanged = existing.MaintenanceItem != updated.MaintenanceItem;

        await using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            existing.MaintenanceItem = updated.MaintenanceItem;
            existing.Description = updated.Description;
            existing.IntervalMileage = updated.IntervalMileage;
            existing.IntervalMonths = updated.IntervalMonths;

            if (serviceChanged)
            {
                // The linked records belong to the old service type, so they no longer match.
                var oldRecords = await context.MaintenanceRecords
                    .Where(r => r.MaintenanceScheduleId == existing.Id)
                    .ToListAsync();
                foreach (var record in oldRecords)
                {
                    record.MaintenanceScheduleId = null;
                }
            }

            // Link earlier records of this service type that have no schedule yet.
            await LinkUnscheduledRecordsAsync(existing);

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
    /// Deletes a schedule. Its maintenance records are kept; the database sets their
    /// MaintenanceScheduleId to null. Returns false if the schedule does not exist
    /// or belongs to another user.
    /// </summary>
    public async Task<bool> DeleteScheduleAsync(int scheduleId, string userId)
    {
        var schedule = await context.MaintenanceSchedules
            .FirstOrDefaultAsync(s => s.Id == scheduleId && s.Vehicle!.UserId == userId);

        if (schedule is null)
        {
            return false;
        }

        try
        {
            context.MaintenanceSchedules.Remove(schedule);
            await context.SaveChangesAsync();
        }
        catch
        {
            context.ChangeTracker.Clear();
            throw;
        }
        return true;
    }

    /// <summary>
    /// Links the vehicle's records of the schedule's service type that have no schedule yet.
    /// "Other" is never linked automatically, because its notes tell the services apart.
    /// The changes are saved by the caller.
    /// </summary>
    private async Task LinkUnscheduledRecordsAsync(MaintenanceSchedule schedule)
    {
        if (schedule.MaintenanceItem == MaintenanceItem.Other)
        {
            return;
        }

        var records = await context.MaintenanceRecords
            .Where(r => r.VehicleId == schedule.VehicleId
                && r.MaintenanceItem == schedule.MaintenanceItem
                && r.MaintenanceScheduleId == null)
            .ToListAsync();

        foreach (var record in records)
        {
            record.MaintenanceScheduleId = schedule.Id;
        }
    }

    /// <summary>
    /// Applies the business rules for schedules.
    /// Throws an exception with a user-friendly message if a rule fails.
    /// </summary>
    private async Task ValidateAsync(MaintenanceSchedule schedule, string userId)
    {
        bool ownsVehicle = await context.Vehicles
            .AnyAsync(v => v.Id == schedule.VehicleId && v.UserId == userId);
        if (!ownsVehicle)
        {
            throw new UnauthorizedAccessException("The vehicle does not belong to the current user.");
        }

        if (schedule.IntervalMileage is null && schedule.IntervalMonths is null)
        {
            throw new ArgumentException("Enter a mileage interval, a time interval, or both.");
        }

        if (schedule.IntervalMileage is < 100 or > 100000)
        {
            throw new ArgumentException("The mileage interval must be between 100 and 100,000 miles.");
        }

        if (schedule.IntervalMonths is < 1 or > 24)
        {
            throw new ArgumentException("The time interval must be between 1 and 24 months.");
        }

        if (schedule.MaintenanceItem == MaintenanceItem.Other && string.IsNullOrWhiteSpace(schedule.Description))
        {
            throw new ArgumentException("A description is required when the service is Other.");
        }

        // One schedule per service type and vehicle, so each record links to only one schedule.
        // "Other" is allowed more than once, because its description tells the schedules apart.
        // When editing, the schedule itself is ignored.
        if (schedule.MaintenanceItem != MaintenanceItem.Other)
        {
            bool isDuplicate = await context.MaintenanceSchedules.AnyAsync(s =>
                s.VehicleId == schedule.VehicleId &&
                s.MaintenanceItem == schedule.MaintenanceItem &&
                s.Id != schedule.Id);

            if (isDuplicate)
            {
                var serviceName = DisplayNameHelper.ToDisplayName(schedule.MaintenanceItem);
                throw new ArgumentException(
                    $"This vehicle already has a schedule for {serviceName}. Please edit that schedule instead.");
            }
        }
    }
}