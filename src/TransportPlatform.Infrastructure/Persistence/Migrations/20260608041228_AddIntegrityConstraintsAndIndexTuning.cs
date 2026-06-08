using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransportPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIntegrityConstraintsAndIndexTuning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_payment_booking_BookingId",
                table: "payment");

            migrationBuilder.DropForeignKey(
                name: "FK_refund_payment_PaymentId",
                table: "refund");

            migrationBuilder.DropIndex(
                name: "IX_trip_CompanyId",
                table: "trip");

            migrationBuilder.DropIndex(
                name: "IX_booking_CreatedBy",
                table: "booking");

            migrationBuilder.DropIndex(
                name: "IX_booking_Status",
                table: "booking");

            migrationBuilder.DropIndex(
                name: "IX_booking_TripId",
                table: "booking");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_CompanyId",
                table: "AspNetUsers");

            migrationBuilder.CreateIndex(
                name: "IX_trip_CompanyId_DepartureUtc",
                table: "trip",
                columns: new[] { "CompanyId", "DepartureUtc" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_trip_currency_format",
                table: "trip",
                sql: "\"Currency\" ~ '^[A-Z]{3}$'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_trip_price_nonneg",
                table: "trip",
                sql: "\"Price\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_trip_status_valid",
                table: "trip",
                sql: "\"Status\" IN ('Scheduled', 'InProgress', 'Completed', 'Cancelled')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_review_rating_range",
                table: "review",
                sql: "\"Rating\" BETWEEN 1 AND 5");

            migrationBuilder.CreateIndex(
                name: "IX_refund_BookingId",
                table: "refund",
                column: "BookingId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_refund_amount_nonneg",
                table: "refund",
                sql: "\"Amount\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_refund_currency_format",
                table: "refund",
                sql: "\"Currency\" ~ '^[A-Z]{3}$'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_refund_status_valid",
                table: "refund",
                sql: "\"Status\" IN ('Pending', 'Completed', 'Failed')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_promo_code_discount_nonneg",
                table: "promo_code",
                sql: "\"DiscountValue\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_payment_amount_nonneg",
                table: "payment",
                sql: "\"Amount\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_payment_currency_format",
                table: "payment",
                sql: "\"Currency\" ~ '^[A-Z]{3}$'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_payment_status_valid",
                table: "payment",
                sql: "\"Status\" IN ('Pending', 'Completed', 'Failed', 'Refunded')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_company_status_valid",
                table: "company",
                sql: "\"Status\" IN ('Pending', 'Active', 'Suspended')");

            migrationBuilder.CreateIndex(
                name: "IX_booking_CreatedBy_CreatedAtUtc",
                table: "booking",
                columns: new[] { "CreatedBy", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_booking_TripId_Status",
                table: "booking",
                columns: new[] { "TripId", "Status" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_booking_amounts_nonneg",
                table: "booking",
                sql: "\"TotalAmount\" >= 0 AND \"DiscountAmount\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_booking_currency_format",
                table: "booking",
                sql: "\"Currency\" ~ '^[A-Z]{3}$'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_booking_status_valid",
                table: "booking",
                sql: "\"Status\" IN ('PendingPayment', 'Confirmed', 'Cancelled', 'Expired')");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_CompanyId_StaffType",
                table: "AspNetUsers",
                columns: new[] { "CompanyId", "StaffType" });

            migrationBuilder.AddForeignKey(
                name: "FK_payment_booking_BookingId",
                table: "payment",
                column: "BookingId",
                principalTable: "booking",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_refund_booking_BookingId",
                table: "refund",
                column: "BookingId",
                principalTable: "booking",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_refund_payment_PaymentId",
                table: "refund",
                column: "PaymentId",
                principalTable: "payment",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_payment_booking_BookingId",
                table: "payment");

            migrationBuilder.DropForeignKey(
                name: "FK_refund_booking_BookingId",
                table: "refund");

            migrationBuilder.DropForeignKey(
                name: "FK_refund_payment_PaymentId",
                table: "refund");

            migrationBuilder.DropIndex(
                name: "IX_trip_CompanyId_DepartureUtc",
                table: "trip");

            migrationBuilder.DropCheckConstraint(
                name: "CK_trip_currency_format",
                table: "trip");

            migrationBuilder.DropCheckConstraint(
                name: "CK_trip_price_nonneg",
                table: "trip");

            migrationBuilder.DropCheckConstraint(
                name: "CK_trip_status_valid",
                table: "trip");

            migrationBuilder.DropCheckConstraint(
                name: "CK_review_rating_range",
                table: "review");

            migrationBuilder.DropIndex(
                name: "IX_refund_BookingId",
                table: "refund");

            migrationBuilder.DropCheckConstraint(
                name: "CK_refund_amount_nonneg",
                table: "refund");

            migrationBuilder.DropCheckConstraint(
                name: "CK_refund_currency_format",
                table: "refund");

            migrationBuilder.DropCheckConstraint(
                name: "CK_refund_status_valid",
                table: "refund");

            migrationBuilder.DropCheckConstraint(
                name: "CK_promo_code_discount_nonneg",
                table: "promo_code");

            migrationBuilder.DropCheckConstraint(
                name: "CK_payment_amount_nonneg",
                table: "payment");

            migrationBuilder.DropCheckConstraint(
                name: "CK_payment_currency_format",
                table: "payment");

            migrationBuilder.DropCheckConstraint(
                name: "CK_payment_status_valid",
                table: "payment");

            migrationBuilder.DropCheckConstraint(
                name: "CK_company_status_valid",
                table: "company");

            migrationBuilder.DropIndex(
                name: "IX_booking_CreatedBy_CreatedAtUtc",
                table: "booking");

            migrationBuilder.DropIndex(
                name: "IX_booking_TripId_Status",
                table: "booking");

            migrationBuilder.DropCheckConstraint(
                name: "CK_booking_amounts_nonneg",
                table: "booking");

            migrationBuilder.DropCheckConstraint(
                name: "CK_booking_currency_format",
                table: "booking");

            migrationBuilder.DropCheckConstraint(
                name: "CK_booking_status_valid",
                table: "booking");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_CompanyId_StaffType",
                table: "AspNetUsers");

            migrationBuilder.CreateIndex(
                name: "IX_trip_CompanyId",
                table: "trip",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_booking_CreatedBy",
                table: "booking",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_booking_Status",
                table: "booking",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_booking_TripId",
                table: "booking",
                column: "TripId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_CompanyId",
                table: "AspNetUsers",
                column: "CompanyId");

            migrationBuilder.AddForeignKey(
                name: "FK_payment_booking_BookingId",
                table: "payment",
                column: "BookingId",
                principalTable: "booking",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_refund_payment_PaymentId",
                table: "refund",
                column: "PaymentId",
                principalTable: "payment",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
