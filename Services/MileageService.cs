using Microsoft.EntityFrameworkCore;
using PerfectRide.Data;

namespace PerfectRide.Services;

public class MileageService(ApplicationDbContext context)
{
    public async Task<List<MileageTracking>> GetMileageForVehicle(int vehicleId, string userId)
    {
        return await context.MileageTrackings
        .AsNoTracking()
        .Where(m => m.VehicleId == vehicleId && m.Vehicle!.UserId == userId)
        .OrderByDescending(m => m.DateRecorded)
        .ToListAsync();
    }

}