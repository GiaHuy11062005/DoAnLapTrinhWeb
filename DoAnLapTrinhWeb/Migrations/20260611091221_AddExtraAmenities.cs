using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoAnLapTrinhWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddExtraAmenities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasAirConditioner",
                table: "Accommodations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasKitchen",
                table: "Accommodations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasLaundryArea",
                table: "Accommodations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasPrivateBathroom",
                table: "Accommodations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasSecurity",
                table: "Accommodations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasWifi",
                table: "Accommodations",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasAirConditioner",
                table: "Accommodations");

            migrationBuilder.DropColumn(
                name: "HasKitchen",
                table: "Accommodations");

            migrationBuilder.DropColumn(
                name: "HasLaundryArea",
                table: "Accommodations");

            migrationBuilder.DropColumn(
                name: "HasPrivateBathroom",
                table: "Accommodations");

            migrationBuilder.DropColumn(
                name: "HasSecurity",
                table: "Accommodations");

            migrationBuilder.DropColumn(
                name: "HasWifi",
                table: "Accommodations");
        }
    }
}
