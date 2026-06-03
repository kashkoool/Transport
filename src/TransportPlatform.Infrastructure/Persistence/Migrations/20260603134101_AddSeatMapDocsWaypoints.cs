using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransportPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSeatMapDocsWaypoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DocumentNumber",
                table: "passenger",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DocumentType",
                table: "passenger",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SeatsPerRow",
                table: "bus",
                type: "integer",
                nullable: false,
                defaultValue: 4);

            migrationBuilder.CreateTable(
                name: "trip_stop",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TripId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ArrivalUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DepartureUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trip_stop", x => x.Id);
                    table.ForeignKey(
                        name: "FK_trip_stop_trip_TripId",
                        column: x => x.TripId,
                        principalTable: "trip",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_trip_stop_TripId_Sequence",
                table: "trip_stop",
                columns: new[] { "TripId", "Sequence" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "trip_stop");

            migrationBuilder.DropColumn(
                name: "DocumentNumber",
                table: "passenger");

            migrationBuilder.DropColumn(
                name: "DocumentType",
                table: "passenger");

            migrationBuilder.DropColumn(
                name: "SeatsPerRow",
                table: "bus");
        }
    }
}
