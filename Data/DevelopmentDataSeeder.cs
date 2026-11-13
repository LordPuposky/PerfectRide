
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
        foreach (var vehicle in vehicles)
        {
            await SeedVehicleDataAsync(db, vehicle);
        }
    }

    private static async Task SeedVehicleDataAsync(
        ApplicationDbContext db,
        Vehicle vehicle)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        // Establish a reasonable starting odometer reading.
        var startingMileage =
            30000 + Math.Max(0, DateTime.Today.Year - vehicle.Year) * 10000;

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
        // MILEAGE TRACKING
        // -----------------------------------------

        // Each fuel and maintenance record gets its
        // own mileage tracking entry.

        var manualMileage = new MileageTracking
        {
            VehicleId = vehicle.Id,
            DateRecorded = today.AddDays(-180),
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

        var maintenanceMileage1 = new MileageTracking
        {
            VehicleId = vehicle.Id,
            DateRecorded = today.AddDays(-105),
            Mileage = startingMileage + 1800,
            Source = MileageSource.MaintenanceRecord
        };

        var fuelMileage2 = new MileageTracking
        {
            VehicleId = vehicle.Id,
            DateRecorded = today.AddDays(-75),
            Mileage = startingMileage + 2500,
            Source = MileageSource.FuelRecord
        };

        var maintenanceMileage2 = new MileageTracking
        {
            VehicleId = vehicle.Id,
            DateRecorded = today.AddDays(-45),
            Mileage = startingMileage + 3300,
            Source = MileageSource.MaintenanceRecord
        };

        var fuelMileage3 = new MileageTracking
        {
            VehicleId = vehicle.Id,
            DateRecorded = today.AddDays(-15),
            Mileage = startingMileage + 4200,
            Source = MileageSource.FuelRecord
        };

        await db.MileageTrackings.AddRangeAsync(
            manualMileage,
            fuelMileage1,
            maintenanceMileage1,
            fuelMileage2,
            maintenanceMileage2,
            fuelMileage3);

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
                DatePerformed = today.AddDays(-105),
                Description = "Routine oil and filter replacement",
                Cost = 69.95m,
                ServiceProvider = "Sample Auto Service",
                MaintenanceScheduleId = oilSchedule.Id,
                MileageTrackingId = maintenanceMileage1.Id
            },
            new()
            {
                VehicleId = vehicle.Id,
                MaintenanceItem = MaintenanceItem.TireRotation,
                DatePerformed = today.AddDays(-45),
                Description = "Tires rotated and inspected",
                Cost = 35.00m,
                ServiceProvider = "Sample Tire Center",
                MaintenanceScheduleId = tireSchedule.Id,
                MileageTrackingId = maintenanceMileage2.Id
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
