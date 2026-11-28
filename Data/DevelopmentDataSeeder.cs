
using Microsoft.EntityFrameworkCore;

namespace PerfectRide.Data;

// Data for development and testing purposes.
public static class DevelopmentDataSeeder
{
    // December 9, 2026 is the assignment demonstration target date.
    private static readonly DateOnly ReferenceDate = new(2026, 12, 9);

    // Vehicle-specific mileage and fuel information.
    private sealed record VehicleMileageProfile(
        int StartingMileage,
        int BrakeOffset,
        int OilOffset,
        int TireOffset,
        int Fuel1Offset,
        int Fuel2Offset,
        int Fuel3Offset,
        decimal Fuel2Gallons,
        decimal Fuel3Gallons);

    public static async Task SeedAsync(IServiceProvider services, string userId)
    {
        using var scope = services.CreateScope();

        var serviceProvider = scope.ServiceProvider;

        var environment = serviceProvider
            .GetRequiredService<IHostEnvironment>();

        var configuration = serviceProvider
            .GetRequiredService<IConfiguration>();

        // Allow seeding in Development or when explicitly enabled for the hosted demo.
        if (!environment.IsDevelopment() &&
            !configuration.GetValue<bool>("DemoData:Enabled"))
        {
            return;
        }

        var db = serviceProvider.GetRequiredService<ApplicationDbContext>();

        // Get this user's vehicles.
        var vehicles = await db.Vehicles
            .Where(v => v.UserId == userId)
            .ToListAsync();

        // Create sample vehicles if this user has none.
        if (vehicles.Count == 0)
        {
            vehicles = new List<Vehicle>
            {
                new() { Year = 2018, Make = "Toyota", Model = "Camry", UserId = userId },
                new() { Year = 2021, Make = "Ford", Model = "F-150", UserId = userId },
                new() { Year = 2016, Make = "Honda", Model = "CR-V", UserId = userId },
                new() { Year = 2023, Make = "Subaru", Model = "Outback", UserId = userId }
            };

            await db.Vehicles.AddRangeAsync(vehicles);
            await db.SaveChangesAsync();
        }

        // Seed related data separately for each vehicle.
        for (int i = 0; i < vehicles.Count; i++)
        {
            var vehicle = vehicles[i];

            // Skip vehicles that already have related data.
            bool hasRelatedData =
                await db.MaintenanceSchedules.AnyAsync(s => s.VehicleId == vehicle.Id) ||
                await db.MaintenanceRecords.AnyAsync(r => r.VehicleId == vehicle.Id) ||
                await db.FuelRecords.AnyAsync(f => f.VehicleId == vehicle.Id) ||
                await db.MileageTrackings.AnyAsync(m => m.VehicleId == vehicle.Id);

            if (hasRelatedData)
                continue;

            await SeedVehicleDataAsync(db, vehicle, i);
        }
    }

    private static async Task SeedVehicleDataAsync(
        ApplicationDbContext db,
        Vehicle vehicle,
        int vehicleIndex)
    {
        // Each vehicle's related data is saved as one transaction.
        await using var transaction = await db.Database.BeginTransactionAsync();

        // -----------------------------------------
        // VEHICLE-SPECIFIC MILEAGE PROFILES
        // -----------------------------------------

        var profile = vehicleIndex switch
        {
            // Toyota Camry
            0 => new VehicleMileageProfile(
                82000,
                350, 650, 850,
                1100, 1550, 2000,
                14.5m, 14.0m),

            // Ford F-150
            1 => new VehicleMileageProfile(
                76000,
                300, 900, 1600,
                1300, 1800, 2400,
                20.0m, 21.0m),

            // Honda CR-V
            2 => new VehicleMileageProfile(
                112000,
                250, 1400, 800,
                1200, 1700, 2050,
                13.5m, 13.8m),

            // Subaru Outback
            _ => new VehicleMileageProfile(
                28000,
                0, 1400, 800,
                1100, 1700, 2000,
                14.0m, 15.7m)
        };

        int startingMileage = profile.StartingMileage;

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
        // MAINTENANCE TEST DATES
        // -----------------------------------------

        // Dates are selected to demonstrate different schedule
        // statuses on December 9, 2026.

        var (oilDate, tireDate, brakeDate) = vehicleIndex switch
        {
            // Toyota: oil overdue, tires due soon, brakes OK.
            0 => (
                ReferenceDate.AddMonths(-7),
                ReferenceDate.AddMonths(-6).AddDays(15),
                ReferenceDate.AddMonths(-8)
            ),

            // Ford: oil due soon, tires OK, brakes overdue.
            1 => (
                ReferenceDate.AddMonths(-6).AddDays(15),
                ReferenceDate.AddMonths(-3),
                ReferenceDate.AddMonths(-13)
            ),

            // Honda: oil OK, tires overdue, brakes due soon.
            2 => (
                ReferenceDate.AddMonths(-3),
                ReferenceDate.AddMonths(-7),
                ReferenceDate.AddMonths(-12).AddDays(15)
            ),

            // Subaru: oil OK, tires due soon, no brake service.
            _ => (
                ReferenceDate.AddMonths(-3),
                ReferenceDate.AddMonths(-6).AddDays(15),
                ReferenceDate.AddMonths(-8)
            )
        };

        int oilMileageValue = startingMileage + profile.OilOffset;
        int tireMileageValue = startingMileage + profile.TireOffset;
        int brakeMileageValue = startingMileage + profile.BrakeOffset;

        // -----------------------------------------
        // MILEAGE TRACKING
        // -----------------------------------------

        // Each fuel and maintenance record gets its own
        // MileageTracking entry.

        var manualMileage = new MileageTracking
        {
            VehicleId = vehicle.Id,
            DateRecorded = ReferenceDate.AddDays(-500),
            Mileage = startingMileage,
            Source = MileageSource.Manual
        };

        var fuelMileage1 = new MileageTracking
        {
            VehicleId = vehicle.Id,
            DateRecorded = ReferenceDate.AddDays(-135),
            Mileage = startingMileage + profile.Fuel1Offset,
            Source = MileageSource.FuelRecord
        };

        var fuelMileage2 = new MileageTracking
        {
            VehicleId = vehicle.Id,
            DateRecorded = ReferenceDate.AddDays(-75),
            Mileage = startingMileage + profile.Fuel2Offset,
            Source = MileageSource.FuelRecord
        };

        var fuelMileage3 = new MileageTracking
        {
            VehicleId = vehicle.Id,
            DateRecorded = ReferenceDate.AddDays(-15),
            Mileage = startingMileage + profile.Fuel3Offset,
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

        var mileageEntries = new List<MileageTracking>
        {
            manualMileage,
            fuelMileage1,
            fuelMileage2,
            fuelMileage3,
            oilMileage,
            tireMileage
        };

        // The Subaru's brake schedule intentionally has no service record.
        bool includeBrakeRecord = vehicleIndex != 3;

        MileageTracking? brakeMileage = null;

        if (includeBrakeRecord)
        {
            brakeMileage = new MileageTracking
            {
                VehicleId = vehicle.Id,
                DateRecorded = brakeDate,
                Mileage = brakeMileageValue,
                Source = MileageSource.MaintenanceRecord
            };

            mileageEntries.Add(brakeMileage);
        }

        await db.MileageTrackings.AddRangeAsync(mileageEntries);

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
                DateFilled = ReferenceDate.AddDays(-135),
                Gallons = 15.200m,
                PricePerGallon = 3.390m,
                TotalCost = 51.53m,
                IsFullTank = true,
                MileageTrackingId = fuelMileage1.Id
            },
            new()
            {
                VehicleId = vehicle.Id,
                DateFilled = ReferenceDate.AddDays(-75),
                Gallons = profile.Fuel2Gallons,
                PricePerGallon = 3.450m,
                TotalCost = decimal.Round(profile.Fuel2Gallons * 3.450m, 2),
                IsFullTank = true,
                MileageTrackingId = fuelMileage2.Id
            },
            new()
            {
                VehicleId = vehicle.Id,
                DateFilled = ReferenceDate.AddDays(-15),
                Gallons = profile.Fuel3Gallons,
                PricePerGallon = 3.290m,
                TotalCost = decimal.Round(profile.Fuel3Gallons * 3.290m, 2),
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
            }
        };

        if (includeBrakeRecord && brakeMileage != null)
        {
            maintenanceRecords.Add(new MaintenanceRecord
            {
                VehicleId = vehicle.Id,
                MaintenanceItem = MaintenanceItem.BrakeInspection,
                DatePerformed = brakeDate,
                Description = "Brake pads, rotors, and fluid inspected",
                Cost = 89.95m,
                ServiceProvider = "Sample Auto Service",
                MaintenanceScheduleId = brakeSchedule.Id,
                MileageTrackingId = brakeMileage.Id
            });
        }

        await db.MaintenanceRecords.AddRangeAsync(maintenanceRecords);
        await db.SaveChangesAsync();

        await transaction.CommitAsync();
    }
}
