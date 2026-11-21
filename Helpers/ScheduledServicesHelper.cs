using PerfectRide.Data;

public class ScheduledServicesHelper
{


    public static (int, List<MaintenanceSchedule>, int, List<MaintenanceSchedule>) CalculateScheduledServices(
        List<MaintenanceSchedule> maintenanceSchedules,
        List<MaintenanceRecord> maintenanceRecords,
        List<MileageTracking> mileageTrackings,
        Dictionary<int, int> vehicleCurrentMileages)
    {
        List<MaintenanceSchedule> overdueServices = new();
        List<MaintenanceSchedule> upcomingServices = new();
        List<MaintenanceRecord> maintenanceRecordsWithSchedules = new();

        maintenanceRecordsWithSchedules = maintenanceRecords
        .Where(r => r.MaintenanceScheduleId != null)
        .ToList();
        if (maintenanceSchedules.Count > 0)
        {

            foreach (MaintenanceSchedule s in maintenanceSchedules)
            {
                int vehicleCurrentMileage =
                vehicleCurrentMileages.TryGetValue(s.VehicleId, out int mileage)
                    ? mileage
                    : 0;
                int scheduleId = s.Id;
                if (s.IntervalMonths != null)
                {
                    List<MaintenanceRecord> associatedRecordsByDate = maintenanceRecordsWithSchedules
                        .Where(r => r.MaintenanceScheduleId == scheduleId)
                        .OrderByDescending(r => r.DatePerformed)
                        .ToList();

                    if (associatedRecordsByDate.Count > 0)
                    {
                        DateOnly serviceLastPerformedDate = associatedRecordsByDate[0].DatePerformed;
                        DateOnly serviceDue = serviceLastPerformedDate.AddMonths(s.IntervalMonths.Value);

                        if (DateOnly.FromDateTime(DateTime.Now) > serviceDue)
                        {
                            overdueServices.Add(s);
                        }
                        if (serviceDue.DayNumber - DateOnly.FromDateTime(DateTime.Now).DayNumber < 30)
                        {
                            upcomingServices.Add(s);
                        }
                    }
                }
                if (s.IntervalMileage != null)
                {
                    List<MaintenanceRecord> associatedRecordsByMileage = maintenanceRecordsWithSchedules
                        .Where(r => r.MaintenanceScheduleId == scheduleId)
                        .Where(r => mileageTrackings.Any(m => m.Id == r.MileageTrackingId))
                        .OrderByDescending(r => mileageTrackings
                        .First(m => m.Id == r.MileageTrackingId).Mileage)
                        .ToList();
                    if (associatedRecordsByMileage.Count > 0)
                    {
                        int mileageLastPerformed = mileageTrackings!
                        .First(r => associatedRecordsByMileage[0].MileageTrackingId == r.Id).Mileage;

                        int mileageDue = mileageLastPerformed + s.IntervalMileage.Value;

                        if (mileageDue < vehicleCurrentMileage && !overdueServices.Contains(s))
                        {
                            overdueServices.Add(s);
                        }
                        if (vehicleCurrentMileage + 500 >= mileageDue && !upcomingServices.Contains(s))
                        {
                            upcomingServices.Add(s);
                        }
                    }
                }
            }
        }
        return (overdueServices.Count, overdueServices, upcomingServices.Count, upcomingServices);
    }
}