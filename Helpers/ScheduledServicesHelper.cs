using PerfectRide.Data;
using PerfectRide.Services;

// Service to help evaluate scheduled services for overdue or upcoming
public class ScheduledServicesHelper(MileageTrackingService mileageTrackingService)
{

    /* Primary method: 
          Input: 
            a list of <maintenanceSchedule>s for a single vehicle
            a list of <maintenanceRecord>s for a single vehicle
            a list of <mileageTracking> records for a single vehicle
            a dictionary containing all of a vehicle's mileage entries matched to an index
          Output: 
            a list of <maintenanceSchedule>s that contain overdue services
            the number of overdue services
            a list of <maintenanceSchedule>s that contain upcoming services(30 days)
            the number of upcoming services*/


    public async Task<(int, List<MaintenanceSchedule>, int, List<MaintenanceSchedule>)> CalculateScheduledServices(
        List<MaintenanceSchedule> maintenanceSchedules,
        List<MaintenanceRecord> maintenanceRecords,
        List<MileageTracking> mileageTrackings,
        Dictionary<int, int> vehicleCurrentMileages)
    {
        List<MaintenanceSchedule> uncompletedSchedules = new();
        List<MaintenanceSchedule> overdueServices = new();
        List<MaintenanceSchedule> upcomingServices = new();
        List<MaintenanceRecord> maintenanceRecordsWithSchedules = new();



        // Extract only maintenanceRecords which have an associated Maintenance Schedule
        maintenanceRecordsWithSchedules = maintenanceRecords
        .Where(r => r.MaintenanceScheduleId != null)
        .ToList();
        Console.WriteLine("****************************************************************************************");
        Console.WriteLine(maintenanceSchedules.Count);
        Console.WriteLine(maintenanceRecordsWithSchedules.Count);
        Console.WriteLine(uncompletedSchedules.Count);


        // Extract only maintenanceSchedules with no associated Maintenance Records
        uncompletedSchedules = maintenanceSchedules
        .Where(s => !maintenanceRecordsWithSchedules
        .Any(r => r.MaintenanceScheduleId == s.Id))
        .ToList();


        if (maintenanceSchedules.Count > 0)
        {

            /*Loop through maintenanceSchedules */
            foreach (MaintenanceSchedule s in maintenanceSchedules)
            {
                // Find current vehicle mileage for associated vehicle
                int vehicleCurrentMileage =
                vehicleCurrentMileages.TryGetValue(s.VehicleId, out int mileage)
                    ? mileage
                    : 0;
                int scheduleId = s.Id;

                // Determine if the maintenanceSchedule includes an interval based on time
                if (s.IntervalMonths != null)
                {
                    /*Gather maintenanceRecords associated with the schedule being considered.  
                    Put them in a list in descending order by the date performed.*/
                    List<MaintenanceRecord> associatedRecordsByDate = maintenanceRecordsWithSchedules
                        .Where(r => r.MaintenanceScheduleId == scheduleId)
                        .OrderByDescending(r => r.DatePerformed)
                        .ToList();
                    // Ensure there is at least 1 maintenance record for the maintenanceSchedule
                    if (associatedRecordsByDate.Count > 0)
                    {
                        /* Extract the last performed (first on the list) maintenanceRecord's date. 
                        Add the intervalMonths value to the last date performed to determine the 
                        service due date. */
                        DateOnly serviceLastPerformedDate = associatedRecordsByDate[0].DatePerformed;
                        DateOnly serviceDue = serviceLastPerformedDate.AddMonths(s.IntervalMonths.Value);

                        /* If todays date is after the calculated due date, add the MaintenanceRecord
                        to the overdueServices list. */
                        if (DateOnly.FromDateTime(DateTime.Now) > serviceDue)
                        {
                            overdueServices.Add(s);
                        }

                        /* If the number of days between today and the date the service is due  is 
                        less than 30 days, add the service to the upcomingServices list
                        **Note** This list intentionally catches overdue services along with services
                        that are not yet due.  A service should not be removed from the upcoming services 
                        list until the service has been performed.*/
                        if (serviceDue.DayNumber - DateOnly.FromDateTime(DateTime.Now).DayNumber < 30)
                        {
                            upcomingServices.Add(s);
                        }
                    }
                }
                /* Determine if the maintenanceSchedule includes an interval based on mileage.
                **Note** A maintenanceSchedule can contain both a mileage and a date interval.  
                This function includes that are approaching either deadline.  Vehicles approaching
                both deadlines could be duplicated on the list.  This is anticipated and prevented 
                in this function.*/
                if (s.IntervalMileage != null)
                {
                    //Gather maintenanceRecords with associated maintenanceSchedules and order by descending Mileage.
                    List<MaintenanceRecord> associatedRecordsByMileage = maintenanceRecordsWithSchedules
                        .Where(r => r.MaintenanceScheduleId == scheduleId)
                        .Where(r => mileageTrackings.Any(m => m.Id == r.MileageTrackingId))
                        .OrderByDescending(r => mileageTrackings
                        .First(m => m.Id == r.MileageTrackingId).Mileage)
                        .ToList();

                    // Ensure there are records to check.
                    if (associatedRecordsByMileage.Count > 0)
                    {

                        // Retrieve the mileage at the last time the service was performed (first on the list)
                        int mileageLastPerformed = mileageTrackings!
                        .First(r => associatedRecordsByMileage[0].MileageTrackingId == r.Id).Mileage;

                        // Calculate mileage service is due.
                        int mileageDue = mileageLastPerformed + s.IntervalMileage.Value;

                        /* If the current mileage is greater than the mileage due, and the record is not 
                        already on the overdue list, add the list to the record.*/
                        if (mileageDue < vehicleCurrentMileage && !overdueServices.Contains(s))
                        {
                            overdueServices.Add(s);
                        }


                        /* If the mileage the vehicle will have after 500 miles from the current mileage 
                        have passed is greater than or equal to the service required mileage, add the 
                        record to the upcoming services list.  **Note** Vehicles that are overdue for 
                        services will intentionally be included on the upcoming services list.  A vehicle
                        should not be removed from the upcoming services list until the service is completed. 
                        This is intentional.*/
                        if (vehicleCurrentMileage + 500 >= mileageDue && !upcomingServices.Contains(s))
                        {
                            upcomingServices.Add(s);
                        }
                    }

                    /*Handle schedules without service records.  Estimate due dates using beginning and ending dates of 
                    vehicle mileage records. Add uncompleted schedules estimated due using those dates to overdue list.
                    Determine difference between beginning and ending mileage records.  If difference exceeds the mileage 
                    interval add to the overdue list.  Use the same process to estimate upcoming maintenance.*/
                }
            }
            if (uncompletedSchedules.Count() > 0)
            {

                foreach (MaintenanceSchedule sc in uncompletedSchedules)
                {
                    // Estimate based on time interval
                    if (sc.IntervalMonths != null)
                    {
                        (var beginningDate, var endingDate) =
                            await mileageTrackingService.GetDateRangeForVehicle(sc.VehicleId);

                        var monthsElapsed =
                            (endingDate.ToDateTime(TimeOnly.MinValue) -
                             beginningDate.ToDateTime(TimeOnly.MinValue)).TotalDays / 30.4375;

                        if (monthsElapsed >= sc.IntervalMonths.Value)
                        {
                            overdueServices.Add(sc);
                        }

                        // Include overdue services in upcoming list
                        if (monthsElapsed >= sc.IntervalMonths.Value - 1 &&
                            !upcomingServices.Contains(sc))
                        {
                            upcomingServices.Add(sc);
                        }
                    }

                    // Estimate based on mileage interval
                    if (sc.IntervalMileage != null)
                    {
                        List<MileageTracking> vehicleTrackings = mileageTrackings
                            .Where(t => t.VehicleId == sc.VehicleId)
                            .OrderBy(t => t.DateRecorded)
                            .ToList();

                        int beginningMileage = vehicleTrackings.FirstOrDefault()?.Mileage ?? 0;
                        int latestMileage = vehicleTrackings.LastOrDefault()?.Mileage ?? 0;

                        int mileageElapsed = latestMileage - beginningMileage;

                        if (mileageElapsed >= sc.IntervalMileage.Value &&
                            !overdueServices.Contains(sc))
                        {
                            overdueServices.Add(sc);
                        }

                        if (mileageElapsed + 500 >= sc.IntervalMileage.Value &&
                            !upcomingServices.Contains(sc))
                        {
                            upcomingServices.Add(sc);
                        }
                    }
                }
            }
        }
        return (overdueServices.Count, overdueServices, upcomingServices.Count, upcomingServices);
    }
}