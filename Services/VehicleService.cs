using Microsoft.EntityFrameworkCore;
using PerfectRide.Data;

namespace PerfectRide.Services;

public class VehicleService(ApplicationDbContext context)
{
    public async Task<List<Vehicle>> GetVehiclesAsync(string userId)
    {
        return await context.Vehicles
        .AsNoTracking()
        .Where(v => v.UserId == userId)
        .OrderBy(v => v.Year)
        .ThenBy(v => v.Make)
        .ToListAsync();
    }
}