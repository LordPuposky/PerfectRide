using Microsoft.EntityFrameworkCore;
using PerfectRide.Data;

namespace PerfectRide.Services;

public class VehicleService(ApplicationDbContext context)
{
    //Retrieve all of a user's vehicles from the database.
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