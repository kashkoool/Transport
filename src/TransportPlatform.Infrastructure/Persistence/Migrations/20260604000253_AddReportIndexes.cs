using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransportPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReportIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_booking_CreatedAtUtc",
                table: "booking",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_booking_CreatedBy",
                table: "booking",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_booking_Status",
                table: "booking",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_CompanyId",
                table: "AspNetUsers",
                column: "CompanyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_booking_CreatedAtUtc",
                table: "booking");

            migrationBuilder.DropIndex(
                name: "IX_booking_CreatedBy",
                table: "booking");

            migrationBuilder.DropIndex(
                name: "IX_booking_Status",
                table: "booking");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_CompanyId",
                table: "AspNetUsers");
        }
    }
}
