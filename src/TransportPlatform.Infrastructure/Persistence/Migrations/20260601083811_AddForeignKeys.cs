using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransportPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_trip_BusId",
                table: "trip",
                column: "BusId");

            migrationBuilder.CreateIndex(
                name: "IX_booking_TripId",
                table: "booking",
                column: "TripId");

            migrationBuilder.AddForeignKey(
                name: "FK_booking_trip_TripId",
                table: "booking",
                column: "TripId",
                principalTable: "trip",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_bus_company_CompanyId",
                table: "bus",
                column: "CompanyId",
                principalTable: "company",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_payment_booking_BookingId",
                table: "payment",
                column: "BookingId",
                principalTable: "booking",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_seat_assignment_trip_TripId",
                table: "seat_assignment",
                column: "TripId",
                principalTable: "trip",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_seat_hold_trip_TripId",
                table: "seat_hold",
                column: "TripId",
                principalTable: "trip",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_trip_bus_BusId",
                table: "trip",
                column: "BusId",
                principalTable: "bus",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_trip_company_CompanyId",
                table: "trip",
                column: "CompanyId",
                principalTable: "company",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_booking_trip_TripId",
                table: "booking");

            migrationBuilder.DropForeignKey(
                name: "FK_bus_company_CompanyId",
                table: "bus");

            migrationBuilder.DropForeignKey(
                name: "FK_payment_booking_BookingId",
                table: "payment");

            migrationBuilder.DropForeignKey(
                name: "FK_seat_assignment_trip_TripId",
                table: "seat_assignment");

            migrationBuilder.DropForeignKey(
                name: "FK_seat_hold_trip_TripId",
                table: "seat_hold");

            migrationBuilder.DropForeignKey(
                name: "FK_trip_bus_BusId",
                table: "trip");

            migrationBuilder.DropForeignKey(
                name: "FK_trip_company_CompanyId",
                table: "trip");

            migrationBuilder.DropIndex(
                name: "IX_trip_BusId",
                table: "trip");

            migrationBuilder.DropIndex(
                name: "IX_booking_TripId",
                table: "booking");
        }
    }
}
