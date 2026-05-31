using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransportPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRefreshTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_seat_hold_TripId_SeatNumber",
                table: "seat_hold");

            migrationBuilder.CreateTable(
                name: "refresh_token",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReplacedByHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_token", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_seat_hold_TripId_SeatNumber",
                table: "seat_hold",
                columns: new[] { "TripId", "SeatNumber" },
                unique: true,
                filter: "\"Consumed\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_token_TokenHash",
                table: "refresh_token",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_refresh_token_UserId",
                table: "refresh_token",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "refresh_token");

            migrationBuilder.DropIndex(
                name: "IX_seat_hold_TripId_SeatNumber",
                table: "seat_hold");

            migrationBuilder.CreateIndex(
                name: "IX_seat_hold_TripId_SeatNumber",
                table: "seat_hold",
                columns: new[] { "TripId", "SeatNumber" },
                unique: true,
                filter: "consumed = false");
        }
    }
}
