using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransportPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSearchIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_trip_Origin_Destination_DepartureUtc",
                table: "trip");

            migrationBuilder.DropIndex(
                name: "IX_outbox_message_ProcessedAtUtc_OccurredAtUtc",
                table: "outbox_message");

            migrationBuilder.CreateIndex(
                name: "IX_outbox_message_OccurredAtUtc",
                table: "outbox_message",
                column: "OccurredAtUtc",
                filter: "\"ProcessedAtUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_company_Status",
                table: "company",
                column: "Status");

            // Trip route+date search: a FUNCTIONAL, PARTIAL index matching the exact query
            // (lower(origin)=.. AND lower(destination)=.. AND status='Scheduled', ordered by
            // departure). EF's fluent API can't express lower()/partial together, so raw SQL.
            migrationBuilder.Sql(
                "CREATE INDEX \"IX_trip_search\" ON trip " +
                "(lower(\"Origin\"), lower(\"Destination\"), \"DepartureUtc\") " +
                "WHERE \"Status\" = 'Scheduled';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_trip_search\";");

            migrationBuilder.DropIndex(
                name: "IX_outbox_message_OccurredAtUtc",
                table: "outbox_message");

            migrationBuilder.DropIndex(
                name: "IX_company_Status",
                table: "company");

            migrationBuilder.CreateIndex(
                name: "IX_trip_Origin_Destination_DepartureUtc",
                table: "trip",
                columns: new[] { "Origin", "Destination", "DepartureUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_message_ProcessedAtUtc_OccurredAtUtc",
                table: "outbox_message",
                columns: new[] { "ProcessedAtUtc", "OccurredAtUtc" });
        }
    }
}
