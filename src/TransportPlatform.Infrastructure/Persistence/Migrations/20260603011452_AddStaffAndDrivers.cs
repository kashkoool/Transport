using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransportPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffAndDrivers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DriverId",
                table: "bus",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StaffType",
                table: "AspNetUsers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "driver",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    LicenseNumber = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_driver", x => x.Id);
                    table.ForeignKey(
                        name: "FK_driver_company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bus_DriverId",
                table: "bus",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_driver_CompanyId",
                table: "driver",
                column: "CompanyId");

            migrationBuilder.AddForeignKey(
                name: "FK_bus_driver_DriverId",
                table: "bus",
                column: "DriverId",
                principalTable: "driver",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_bus_driver_DriverId",
                table: "bus");

            migrationBuilder.DropTable(
                name: "driver");

            migrationBuilder.DropIndex(
                name: "IX_bus_DriverId",
                table: "bus");

            migrationBuilder.DropColumn(
                name: "DriverId",
                table: "bus");

            migrationBuilder.DropColumn(
                name: "StaffType",
                table: "AspNetUsers");
        }
    }
}
