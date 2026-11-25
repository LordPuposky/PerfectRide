using Microsoft.EntityFrameworkCore;
using PerfectRide.Data;

namespace PerfectRide.Services;


public class MaintenanceScheduleService(ApplicationDbContext context)
{

    //Retreives all of a user's maintenanceSchedules
    public async Task<List<MaintenanceSchedule>> GetMaintenanceScheduleForUser(string userId)
    {
        return await context.MaintenanceSchedules
        .AsNoTracking()
        .Where(r => r.Vehicle!.UserId == userId)
        .ToListAsync();
    }
}