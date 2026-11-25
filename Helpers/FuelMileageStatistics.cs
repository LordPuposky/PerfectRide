using PerfectRide.Data;

namespace PerfectRide.Helpers;

//Helper class to calculate statistics for fuel mileage
public class FuelMileageStatistics
{

    public (decimal, int) GetFuelMileageStatistics(List<FuelRecord> fuelRecords, List<MileageTracking> mileageTrackings, string userId)
    {

        int vehicleTotalFillUps = 0;
        decimal vehicleFuelMileage = 0;

        // Ensure fuel records exist
        if (fuelRecords.Count > 0)
        {
            // Store fill-up count by counting fuel records.
            vehicleTotalFillUps = fuelRecords.Count;

            /* Reset TotalGallons to 0.  Create a list of fuel records where the
            fuel record's MileageTrackingId matches the Id of a MileageTracking record.  
            Sort the records in ascending order by the Mileage reading on the first
            Mileage record with the matching MileageTracking Id*/
            decimal totalGallons = 0;
            List<FuelRecord> sortedRecords = fuelRecords
            .Where(r => mileageTrackings.Any(m => m.Id == r.MileageTrackingId))
            .OrderBy(r => mileageTrackings
            .First(m => m.Id == r.MileageTrackingId).Mileage)
            .ToList();

            // Ensure retrieved records have at least 1 record.
            if (sortedRecords.Count == 0)
            {
                return (0, vehicleTotalFillUps);
            }

            /* Remove fuel records from the beginning of the list if they do not 
            represent a fuel record with a full tank.  Partial tank fill-ups 
            at the beginning or end of a fuel mileage calculation will skew the 
            results and must be removed.*/
            while (sortedRecords[0].IsFullTank == false && sortedRecords.Count > 1)
            {
                sortedRecords.RemoveAt(0);
            }

            /* Retrieve the starting mileage of the first record in the new list
            which represents the mileage at the beginning of the first full tank 
            of fuel for an accurate fuel mileage reading*/
            int startMiles = mileageTrackings
            .First(m => sortedRecords[0].MileageTrackingId == m.Id).Mileage;

            /*  Check to see if the last record in the list represents a 
            full tank.  If it does not, remove it from the list as it will 
            skew the fuel mileage results.  Set the ending mileage at the 
            mileage value of the final record which represents a full tank 
            fill-up*/
            while (sortedRecords.Count > 1 && sortedRecords.Last().IsFullTank == false)
            {
                sortedRecords.RemoveAt(sortedRecords.Count - 1);

            }

            int endMiles = mileageTrackings.First(m => sortedRecords.Last().MileageTrackingId == m.Id).Mileage;

            /* For each record left in the list (except for the first one), take the
            gallons from the record and add it to the total gallons.  The first record
            is not used because it represents the fuel required to bring the tank to a
            full level at the beginning mileage.  This fuel was not used in the tracked 
            period. */
            for (int i = 1; i < sortedRecords.Count; i++)
            {
                totalGallons += sortedRecords[i].Gallons;
            }

            /* Ensure that there is more than one record in the list.  This ensures a
            different beginning and ending mileage reading.  Also ensure that  
            the totalGallons contains a number other than 0 to ensure the calculation
            is protected against the divide by 0 exception.  Next subtract the beginning 
            mileage from the ending mileage.  Divide that result by the total gallons
            to determine the vehicle's average fuel mileage.  */
            if (totalGallons != 0 && startMiles != endMiles)
            {
                int vehicleTotalMileage = endMiles - startMiles;
                vehicleFuelMileage = vehicleTotalMileage / totalGallons;
            }

        }
        return (vehicleFuelMileage, vehicleTotalFillUps);
    }
}
