using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoAnLapTrinhWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddRentalProcessingFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ConfirmedAt",
                table: "RentalRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasPendingNegotiation",
                table: "ChatConversations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "AccommodationType",
                table: "Accommodations",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "AvailableRooms",
                table: "Accommodations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsRentedOut",
                table: "Accommodations",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConfirmedAt",
                table: "RentalRequests");

            migrationBuilder.DropColumn(
                name: "HasPendingNegotiation",
                table: "ChatConversations");

            migrationBuilder.DropColumn(
                name: "AccommodationType",
                table: "Accommodations");

            migrationBuilder.DropColumn(
                name: "AvailableRooms",
                table: "Accommodations");

            migrationBuilder.DropColumn(
                name: "IsRentedOut",
                table: "Accommodations");
        }
    }
}
