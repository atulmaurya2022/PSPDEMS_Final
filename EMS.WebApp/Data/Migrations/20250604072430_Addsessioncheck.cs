using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMS.WebApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class Addsessioncheck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_MedBase_BaseName_Unique",
                table: "med_base",
                column: "base_name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MedBase_BaseName_Unique",
                table: "med_base");
        }
    }
}
