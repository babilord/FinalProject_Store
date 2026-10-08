using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinalProject_Store.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderReservationLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Products",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAtUtc",
                table: "Orders",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "ReservationExpired",
                table: "Orders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Old timestamps were local time, so do not guess their UTC offset.
            // Give existing unpaid reservations one final 30-minute window from deployment.
            migrationBuilder.Sql("UPDATE [Orders] SET [ExpiresAtUtc] = DATEADD(minute, 30, SYSUTCDATETIME()) WHERE [Status] = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_Inventory",
                table: "Products",
                sql: "[Inventory] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Status_ExpiresAtUtc",
                table: "Orders",
                columns: new[] { "Status", "ExpiresAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_Inventory",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Orders_Status_ExpiresAtUtc",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ExpiresAtUtc",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ReservationExpired",
                table: "Orders");
        }
    }
}
