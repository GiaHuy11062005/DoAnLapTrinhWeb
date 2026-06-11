using DoAnLapTrinhWeb.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoAnLapTrinhWeb.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260610170000_AddAccommodationAmenities")]
    public partial class AddAccommodationAmenities : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>("Has24HourSecurity", "Accommodations", "bit", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<bool>("HasBalcony", "Accommodations", "bit", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<bool>("HasBasementParking", "Accommodations", "bit", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<bool>("HasElevator", "Accommodations", "bit", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<bool>("HasHighSpeedWifi", "Accommodations", "bit", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<bool>("HasInverterAirConditioner", "Accommodations", "bit", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<bool>("HasRefrigerator", "Accommodations", "bit", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<bool>("HasWasherDryer", "Accommodations", "bit", nullable: false, defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn("Has24HourSecurity", "Accommodations");
            migrationBuilder.DropColumn("HasBalcony", "Accommodations");
            migrationBuilder.DropColumn("HasBasementParking", "Accommodations");
            migrationBuilder.DropColumn("HasElevator", "Accommodations");
            migrationBuilder.DropColumn("HasHighSpeedWifi", "Accommodations");
            migrationBuilder.DropColumn("HasInverterAirConditioner", "Accommodations");
            migrationBuilder.DropColumn("HasRefrigerator", "Accommodations");
            migrationBuilder.DropColumn("HasWasherDryer", "Accommodations");
        }
    }
}
