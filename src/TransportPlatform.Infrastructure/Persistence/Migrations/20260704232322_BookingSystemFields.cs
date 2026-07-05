using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransportPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BookingSystemFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ActualArrivalUtc",
                table: "trip",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ActualDepartureUtc",
                table: "trip",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LicenseExpiryUtc",
                table: "driver",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "company",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankAccount",
                table: "company",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxId",
                table: "company",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "InsuranceExpiryUtc",
                table: "bus",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LicensePlate",
                table: "bus",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "booking",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActualArrivalUtc",
                table: "trip");

            migrationBuilder.DropColumn(
                name: "ActualDepartureUtc",
                table: "trip");

            migrationBuilder.DropColumn(
                name: "LicenseExpiryUtc",
                table: "driver");

            migrationBuilder.DropColumn(
                name: "Address",
                table: "company");

            migrationBuilder.DropColumn(
                name: "BankAccount",
                table: "company");

            migrationBuilder.DropColumn(
                name: "TaxId",
                table: "company");

            migrationBuilder.DropColumn(
                name: "InsuranceExpiryUtc",
                table: "bus");

            migrationBuilder.DropColumn(
                name: "LicensePlate",
                table: "bus");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "booking");
        }
    }
}
