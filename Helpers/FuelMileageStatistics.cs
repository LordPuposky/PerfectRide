using PerfectRide.Data;

namespace PerfectRide.Helpers;


public class FuelMileageStatistics
{

    public (decimal, int) GetFuelMileageStatistics(List<FuelRecord> fuelRecords, List<MileageTracking> mileageTrackings, string userId)
    {

        int vehicleTotalFillUps = 0;
        decimal vehicleFuelMileage = 0;

        if (fuelRecords.Count > 0)
        {
            vehicleTotalFillUps = fuelRecords.Count;
            decimal totalGallons = 0;
            List<FuelRecord> sortedRecords = fuelRecords
            .Where(r => mileageTrackings.Any(m => m.Id == r.MileageTrackingId))
            .OrderBy(r => mileageTrackings
            .First(m => m.Id == r.MileageTrackingId).Mileage)
            .ToList();

            while (sortedRecords[0].IsFullTank == false && sortedRecords.Count > 1)
            {
                sortedRecords.RemoveAt(0);
            }

            int startMiles = mileageTrackings
            .First(m => sortedRecords[0].MileageTrackingId == m.Id).Mileage;

            {
                while (sortedRecords.Count > 1 && sortedRecords.Last().IsFullTank == false)
                {
                    sortedRecords.RemoveAt(sortedRecords.Count - 1);

                }
            }
            int endMiles = mileageTrackings.First(m => sortedRecords.Last().MileageTrackingId == m.Id).Mileage;

            for (int i = 1; i < sortedRecords.Count; i++)
            {
                totalGallons += sortedRecords[i].Gallons;
            }
            if (totalGallons != 0 && startMiles != endMiles)
            {
                int vehicleTotalMileage = endMiles - startMiles;
                vehicleFuelMileage = vehicleTotalMileage / totalGallons;
            }

        }
        return (vehicleFuelMileage, vehicleTotalFillUps);
    }
}
