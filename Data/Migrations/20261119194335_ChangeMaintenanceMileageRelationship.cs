using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PerfectRide.Migrations
{
    /// <inheritdoc />
    public partial class ChangeMaintenanceMileageRelationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MaintenanceRecords_MileageTrackingId",
                table: "MaintenanceRecords");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRecords_MileageTrackingId",
                table: "MaintenanceRecords",
                column: "MileageTrackingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MaintenanceRecords_MileageTrackingId",
                table: "MaintenanceRecords");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRecords_MileageTrackingId",
                table: "MaintenanceRecords",
                column: "MileageTrackingId",
                unique: true);
        }
    }
}
