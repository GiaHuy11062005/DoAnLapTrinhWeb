using DoAnLapTrinhWeb.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoAnLapTrinhWeb.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260611160000_AddAccommodationMaxOccupants")]
    public partial class AddAccommodationMaxOccupants : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxOccupants",
                table: "Accommodations",
                type: "int",
                nullable: false,
                defaultValue: 1);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "MaxOccupants", table: "Accommodations");
        }
    }
}
