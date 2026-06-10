using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoAnLapTrinhWeb.Migrations
{
    /// <inheritdoc />
    public partial class Timphongtro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Accommodations_CityId",
                table: "Accommodations",
                column: "CityId");

            migrationBuilder.CreateIndex(
                name: "IX_Accommodations_DistrictId",
                table: "Accommodations",
                column: "DistrictId");

            migrationBuilder.AddForeignKey(
                name: "FK_Accommodations_Cities_CityId",
                table: "Accommodations",
                column: "CityId",
                principalTable: "Cities",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.AddForeignKey(
                name: "FK_Accommodations_Districts_DistrictId",
                table: "Accommodations",
                column: "DistrictId",
                principalTable: "Districts",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Accommodations_Cities_CityId",
                table: "Accommodations");

            migrationBuilder.DropForeignKey(
                name: "FK_Accommodations_Districts_DistrictId",
                table: "Accommodations");

            migrationBuilder.DropIndex(
                name: "IX_Accommodations_CityId",
                table: "Accommodations");

            migrationBuilder.DropIndex(
                name: "IX_Accommodations_DistrictId",
                table: "Accommodations");
        }
    }
}
