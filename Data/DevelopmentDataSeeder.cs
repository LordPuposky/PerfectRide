// Data for development and testing purposes. 

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PerfectRide.Data;

namespace PerfectRide.Data;

public static class DevelopmentDataSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();

        var serviceProvider = scope.ServiceProvider;

        var environment = serviceProvider
            .GetRequiredService<IHostEnvironment>();

        // Never seed sample data in production.
        if (!environment.IsDevelopment())
            return;

        var userManager = serviceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        var db = serviceProvider
            .GetRequiredService<ApplicationDbContext>();

        // Find the existing user.
        var user = await userManager.Users.SingleOrDefaultAsync();

        if (user == null)
            return;

        // Get this user's vehicles.
        var vehicles = await db.Vehicles
            .Where(v => v.UserId == user.Id)
            .ToListAsync();

        // Create sample vehicles if none exist.
        if (vehicles.Count == 0)
        {
            vehicles = new List<Vehicle>
            {
                new() { Year = 2018, Make = "Toyota", Model = "Camry", UserId = user.Id },
                new() { Year = 2021, Make = "Ford", Model = "F-150", UserId = user.Id },
                new() { Year = 2016, Make = "Honda", Model = "CR-V", UserId = user.Id },
                new() { Year = 2023, Make = "Subaru", Model = "Outback", UserId = user.Id }
            };

            await db.Vehicles.AddRangeAsync(vehicles);
            await db.SaveChangesAsync();
        }

        // Avoid inserting duplicate related test data.
        var vehicleIds = vehicles.Select(v => v.Id).ToList();

        var hasRelatedData =
            await db.MaintenanceSchedules.AnyAsync(s => vehicleIds.Contains(s.VehicleId)) ||
            await db.MaintenanceRecords.AnyAsync(r => vehicleIds.Contains(r.VehicleId)) ||
            await db.FuelRecords.AnyAsync(f => vehicleIds.Contains(f.VehicleId)) ||
            await db.Documents.AnyAsync(d => vehicleIds.Contains(d.VehicleId)) ||
            await db.MileageTrackings.AnyAsync(m => vehicleIds.Contains(m.VehicleId));

        if (hasRelatedData)
            return;

        // Create sample data for each vehicle.
        for (int i = 0; i < vehicles.Count; i++)
        {
            await SeedVehicleDataAsync(db, vehicles[i], i);
        }
    }

    private static async Task SeedVehicleDataAsync(
        ApplicationDbContext db,
        Vehicle vehicle,
        int vehicleIndex)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        // Establish a reasonable starting odometer reading.
        var startingMileage =
            30000 + Math.Max(0, DateTime.Today.Year - Convert.ToInt32(vehicle.Year)) * 10000;

        // -----------------------------------------
        // MAINTENANCE SCHEDULES
        // -----------------------------------------

        var oilSchedule = new MaintenanceSchedule
        {
            VehicleId = vehicle.Id,
            MaintenanceItem = MaintenanceItem.OilChange,
            Description = "Regular engine oil and filter change",
            IntervalMileage = 5000,
            IntervalMonths = 6
        };

        var tireSchedule = new MaintenanceSchedule
        {
            VehicleId = vehicle.Id,
            MaintenanceItem = MaintenanceItem.TireRotation,
            Description = "Rotate tires and inspect tire condition",
            IntervalMileage = 7500,
            IntervalMonths = 6
        };

        var brakeSchedule = new MaintenanceSchedule
        {
            VehicleId = vehicle.Id,
            MaintenanceItem = MaintenanceItem.BrakeInspection,
            Description = "Inspect brake pads, rotors, and fluid",
            IntervalMonths = 12
        };

        await db.MaintenanceSchedules.AddRangeAsync(
            oilSchedule,
            tireSchedule,
            brakeSchedule);

        await db.SaveChangesAsync();

        // -----------------------------------------
        // MAINTENANCE TEST DATES AND MILEAGES
        // -----------------------------------------

        // Each vehicle has different service dates so we can
        // test overdue, upcoming, and future schedules.

        var (oilDate, oilMileageValue,
             tireDate, tireMileageValue,
             brakeDate, brakeMileageValue) = vehicleIndex switch
             {
                 // Toyota Camry:
                 // Oil overdue, tires upcoming, brakes future.
                 0 => (
                     today.AddMonths(-7), startingMileage + 600,
                     today.AddMonths(-6).AddDays(15), startingMileage + 800,
                     today.AddMonths(-8), startingMileage + 500
                 ),

                 // Ford F-150:
                 // Oil upcoming, tires future, brakes overdue.
                 1 => (
                     today.AddMonths(-6).AddDays(15), startingMileage + 800,
                     today.AddMonths(-3), startingMileage + 1500,
                     today.AddMonths(-13), startingMileage + 100
                 ),

                 // Honda CR-V:
                 // Oil future, tires overdue, brakes upcoming.
                 2 => (
                     today.AddMonths(-3), startingMileage + 1500,
                     today.AddMonths(-7), startingMileage + 650,
                     today.AddMonths(-12).AddDays(15), startingMileage + 300
                 ),

                 // Subaru Outback:
                 // Oil future, tires upcoming, brakes future.
                 _ => (
                     today.AddMonths(-3), startingMileage + 1500,
                     today.AddMonths(-6).AddDays(15), startingMileage + 800,
                     today.AddMonths(-8), startingMileage + 500
                 )
             };

        // -----------------------------------------
        // MILEAGE TRACKING
        // -----------------------------------------

        // Each fuel and maintenance record gets its
        // own mileage tracking entry.

        var manualMileage = new MileageTracking
        {
            VehicleId = vehicle.Id,
            DateRecorded = today.AddDays(-500),
            Mileage = startingMileage,
            Source = MileageSource.Manual
        };

        var fuelMileage1 = new MileageTracking
        {
            VehicleId = vehicle.Id,
            DateRecorded = today.AddDays(-135),
            Mileage = startingMileage + 1000,
            Source = MileageSource.FuelRecord
        };

        var fuelMileage2 = new MileageTracking
        {
            VehicleId = vehicle.Id,
            DateRecorded = today.AddDays(-75),
            Mileage = startingMileage + 2500,
            Source = MileageSource.FuelRecord
        };

        var fuelMileage3 = new MileageTracking
        {
            VehicleId = vehicle.Id,
            DateRecorded = today.AddDays(-15),
            Mileage = startingMileage + 4200,
            Source = MileageSource.FuelRecord
        };

        var oilMileage = new MileageTracking
        {
            VehicleId = vehicle.Id,
            DateRecorded = oilDate,
            Mileage = oilMileageValue,
            Source = MileageSource.MaintenanceRecord
        };

        var tireMileage = new MileageTracking
        {
            VehicleId = vehicle.Id,
            DateRecorded = tireDate,
            Mileage = tireMileageValue,
            Source = MileageSource.MaintenanceRecord
        };

        var brakeMileage = new MileageTracking
        {
            VehicleId = vehicle.Id,
            DateRecorded = brakeDate,
            Mileage = brakeMileageValue,
            Source = MileageSource.MaintenanceRecord
        };

        await db.MileageTrackings.AddRangeAsync(
            manualMileage,
            fuelMileage1,
            fuelMileage2,
            fuelMileage3,
            oilMileage,
            tireMileage,
            brakeMileage);

        // Save first so each mileage entry has an ID.
        await db.SaveChangesAsync();

        // -----------------------------------------
        // FUEL RECORDS
        // -----------------------------------------

        var fuelRecords = new List<FuelRecord>
        {
            new()
            {
                VehicleId = vehicle.Id,
                DateFilled = today.AddDays(-135),
                Gallons = 15.200m,
                PricePerGallon = 3.390m,
                TotalCost = 51.53m,
                IsFullTank = true,
                MileageTrackingId = fuelMileage1.Id
            },
            new()
            {
                VehicleId = vehicle.Id,
                DateFilled = today.AddDays(-75),
                Gallons = 13.800m,
                PricePerGallon = 3.450m,
                TotalCost = 47.61m,
                IsFullTank = true,
                MileageTrackingId = fuelMileage2.Id
            },
            new()
            {
                VehicleId = vehicle.Id,
                DateFilled = today.AddDays(-15),
                Gallons = 14.700m,
                PricePerGallon = 3.290m,
                TotalCost = 48.36m,
                IsFullTank = true,
                MileageTrackingId = fuelMileage3.Id
            }
        };

        await db.FuelRecords.AddRangeAsync(fuelRecords);

        // -----------------------------------------
        // MAINTENANCE RECORDS
        // -----------------------------------------

        var maintenanceRecords = new List<MaintenanceRecord>
        {
            new()
            {
                VehicleId = vehicle.Id,
                MaintenanceItem = MaintenanceItem.OilChange,
                DatePerformed = oilDate,
                Description = "Routine oil and filter replacement",
                Cost = 69.95m,
                ServiceProvider = "Sample Auto Service",
                MaintenanceScheduleId = oilSchedule.Id,
                MileageTrackingId = oilMileage.Id
            },
            new()
            {
                VehicleId = vehicle.Id,
                MaintenanceItem = MaintenanceItem.TireRotation,
                DatePerformed = tireDate,
                Description = "Tires rotated and inspected",
                Cost = 35.00m,
                ServiceProvider = "Sample Tire Center",
                MaintenanceScheduleId = tireSchedule.Id,
                MileageTrackingId = tireMileage.Id
            },
            new()
            {
                VehicleId = vehicle.Id,
                MaintenanceItem = MaintenanceItem.BrakeInspection,
                DatePerformed = brakeDate,
                Description = "Brake pads, rotors, and fluid inspected",
                Cost = 89.95m,
                ServiceProvider = "Sample Auto Service",
                MaintenanceScheduleId = brakeSchedule.Id,
                MileageTrackingId = brakeMileage.Id
            }
        };

        await db.MaintenanceRecords.AddRangeAsync(maintenanceRecords);

        await db.SaveChangesAsync();

        // -----------------------------------------
        // DOCUMENTS
        // -----------------------------------------

        var documents = new List<Document>
        {
            new()
            {
                VehicleId = vehicle.Id,
                DocumentType = DocumentType.Registration,
                Name = "Vehicle Registration",
                FileName = "registration.pdf",
                FileType = FileType.Pdf,
                FilePath = $"seed-data/{vehicle.Id}/registration.pdf",
                DateUploaded = DateTime.Now.AddDays(-150),
                Description = "Sample vehicle registration"
            },
            new()
            {
                VehicleId = vehicle.Id,
                DocumentType = DocumentType.Insurance,
                Name = "Insurance Policy",
                FileName = "insurance.pdf",
                FileType = FileType.Pdf,
                FilePath = $"seed-data/{vehicle.Id}/insurance.pdf",
                DateUploaded = DateTime.Now.AddDays(-90),
                Description = "Sample insurance documentation"
            },
            new()
            {
                VehicleId = vehicle.Id,
                DocumentType = DocumentType.Warranty,
                Name = "Vehicle Warranty",
                FileName = "warranty.pdf",
                FileType = FileType.Pdf,
                FilePath = $"seed-data/{vehicle.Id}/warranty.pdf",
                DateUploaded = DateTime.Now.AddDays(-60),
                Description = "Sample warranty documentation"
            },
            new()
            {
                VehicleId = vehicle.Id,
                DocumentType = DocumentType.Receipt,
                Name = "Maintenance Receipt",
                FileName = "maintenance-receipt.jpg",
                FileType = FileType.Jpg,
                FilePath = $"seed-data/{vehicle.Id}/maintenance-receipt.jpg",
                DateUploaded = DateTime.Now.AddDays(-45),
                Description = "Sample maintenance receipt"
            }
        };

        await db.Documents.AddRangeAsync(documents);

        await db.SaveChangesAsync();
    }
}
