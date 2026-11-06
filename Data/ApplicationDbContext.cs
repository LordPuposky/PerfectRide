using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace PerfectRide.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Vehicle> Vehicles { get; set; } = null!;
    public DbSet<MaintenanceSchedule> MaintenanceSchedules { get; set; } = null!;
    public DbSet<MaintenanceRecord> MaintenanceRecords { get; set; } = null!;
    public DbSet<FuelRecord> FuelRecords { get; set; } = null!;
    public DbSet<Document> Documents { get; set; } = null!;
    public DbSet<MileageTracking> MileageTrackings { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        //FuelRecord -> Mileage Tracking
        modelBuilder.Entity<FuelRecord>()
            .HasOne(f => f.MileageTracking)
            .WithOne(m => m.FuelRecord)
            .HasForeignKey<FuelRecord>(f => f.MileageTrackingId)
            .IsRequired()
            .OnDelete(DeleteBehavior.NoAction);
        //MaintenanceRecord -> MileageTracking
        modelBuilder.Entity<MaintenanceRecord>()
            .HasOne(r => r.MileageTracking)
            .WithOne(m => m.MaintenanceRecord)
            .HasForeignKey<MaintenanceRecord>(r => r.MileageTrackingId)
            .IsRequired()
            .OnDelete(DeleteBehavior.NoAction);
        //No MileageTracking entries with same vehicle, date, and mileage    
        modelBuilder.Entity<MileageTracking>()
            .HasIndex(m => new { m.VehicleId, m.DateRecorded, m.Mileage })
            .IsUnique();
        //Precision settings for decimals    
        modelBuilder.Entity<FuelRecord>()
            .Property(f => f.Gallons)
            .HasPrecision(8, 3);

        modelBuilder.Entity<FuelRecord>()
            .Property(f => f.PricePerGallon)
            .HasPrecision(8, 3);

        modelBuilder.Entity<FuelRecord>()
            .Property(f => f.TotalCost)
            .HasPrecision(10, 2);

        modelBuilder.Entity<MaintenanceRecord>()
            .Property(m => m.Cost)
            .HasPrecision(10, 2);

        // User -> Vehicles
        modelBuilder.Entity<Vehicle>()
            .HasOne(v => v.User)
            .WithMany(u => u.Vehicles)
            .HasForeignKey(v => v.UserId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        // Vehicle -> MaintenanceSchedules
        modelBuilder.Entity<MaintenanceSchedule>()
            .HasOne(s => s.Vehicle)
            .WithMany(v => v.MaintenanceSchedules)
            .HasForeignKey(s => s.VehicleId)
            .OnDelete(DeleteBehavior.Cascade);

        // Vehicle -> MaintenanceRecords
        modelBuilder.Entity<MaintenanceRecord>()
            .HasOne(r => r.Vehicle)
            .WithMany(v => v.MaintenanceRecords)
            .HasForeignKey(r => r.VehicleId)
            .OnDelete(DeleteBehavior.Cascade);

        // MaintenanceSchedule -> MaintenanceRecords
        modelBuilder.Entity<MaintenanceRecord>()
            .HasOne(r => r.MaintenanceSchedule)
            .WithMany(s => s.MaintenanceRecords)
            .HasForeignKey(r => r.MaintenanceScheduleId)
            .OnDelete(DeleteBehavior.SetNull);

        // Vehicle -> FuelRecords
        modelBuilder.Entity<FuelRecord>()
            .HasOne(f => f.Vehicle)
            .WithMany(v => v.FuelRecords)
            .HasForeignKey(f => f.VehicleId)
            .OnDelete(DeleteBehavior.Cascade);

        // Vehicle -> Documents
        modelBuilder.Entity<Document>()
            .HasOne(d => d.Vehicle)
            .WithMany(v => v.Documents)
            .HasForeignKey(d => d.VehicleId)
            .OnDelete(DeleteBehavior.Cascade);

        // Vehicle -> MileageTracking
        modelBuilder.Entity<MileageTracking>()
            .HasOne(m => m.Vehicle)
            .WithMany(v => v.MileageTrackings)
            .HasForeignKey(m => m.VehicleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
