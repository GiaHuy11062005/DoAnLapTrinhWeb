using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoAnLapTrinhWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddAccommodationType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccommodationType",
                table: "Accommodations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetAudience",
                table: "Accommodations",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccommodationType",
                table: "Accommodations");

            migrationBuilder.DropColumn(
                name: "TargetAudience",
                table: "Accommodations");
        }
    }
}
