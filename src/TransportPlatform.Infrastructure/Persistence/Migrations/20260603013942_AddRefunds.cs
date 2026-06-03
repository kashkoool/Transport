using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransportPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRefunds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "refund",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    GatewayRefundRef = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refund", x => x.Id);
                    table.ForeignKey(
                        name: "FK_refund_payment_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "payment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_refund_GatewayRefundRef",
                table: "refund",
                column: "GatewayRefundRef");

            migrationBuilder.CreateIndex(
                name: "IX_refund_IdempotencyKey",
                table: "refund",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_refund_PaymentId",
                table: "refund",
                column: "PaymentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "refund");
        }
    }
}
