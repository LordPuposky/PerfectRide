using Microsoft.EntityFrameworkCore;
using PerfectRide.Data;

namespace PerfectRide.Services;

public class FuelService(ApplicationDbContext context)
{
    //Retrieves the fuel records for a single vehicle from the database.
    public async Task<List<FuelRecord>> GetFuelRecordsForVehicleAsync(int vehicleId, string userId)
    {
        return await context.FuelRecords
        .AsNoTracking()
        .Where(f => f.VehicleId == vehicleId && f.Vehicle!.UserId == userId)
        .OrderByDescending(f => f.DateFilled)
        .ToListAsync();
    }
}