using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransportPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AuthDbHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContactPhone",
                table: "booking",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_refund_BookingId",
                table: "refund",
                column: "BookingId");

            migrationBuilder.AddForeignKey(
                name: "FK_refresh_token_AspNetUsers_UserId",
                table: "refresh_token",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // Partial index to keep the pending-refund reconciliation poll cheap as the refund
            // table grows (only a tiny fraction of rows are ever Pending at once).
            migrationBuilder.Sql("CREATE INDEX \"IX_refund_pending\" ON refund (\"Id\") WHERE \"Status\" = 'Pending';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_refund_pending\";");

            migrationBuilder.DropForeignKey(
                name: "FK_refresh_token_AspNetUsers_UserId",
                table: "refresh_token");

            migrationBuilder.DropIndex(
                name: "IX_refund_BookingId",
                table: "refund");

            migrationBuilder.DropColumn(
                name: "ContactPhone",
                table: "booking");
        }
    }
}
