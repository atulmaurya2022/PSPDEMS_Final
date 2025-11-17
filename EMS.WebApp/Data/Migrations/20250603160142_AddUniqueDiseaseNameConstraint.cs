using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMS.WebApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueDiseaseNameConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_sys_attach_screen_role_sys_screen_name_screen_uid",
                table: "sys_attach_screen_role");

            migrationBuilder.DropIndex(
                name: "IX_sys_attach_screen_role_screen_uid",
                table: "sys_attach_screen_role");

            migrationBuilder.AddColumn<string>(
                name: "adid",
                table: "sys_user",
                type: "varchar(100)",
                unicode: false,
                maxLength: 100,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "screen_uid",
                table: "sys_attach_screen_role",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateTable(
                name: "user_action",
                columns: table => new
                {
                    uid = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    action_type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    time_stamp = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_action", x => x.uid);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MedDisease_DiseaseName_Unique",
                table: "med_disease",
                column: "disease_name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_action");

            migrationBuilder.DropIndex(
                name: "IX_MedDisease_DiseaseName_Unique",
                table: "med_disease");

            migrationBuilder.DropColumn(
                name: "adid",
                table: "sys_user");

            migrationBuilder.AlterColumn<int>(
                name: "screen_uid",
                table: "sys_attach_screen_role",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_sys_attach_screen_role_screen_uid",
                table: "sys_attach_screen_role",
                column: "screen_uid");

            migrationBuilder.AddForeignKey(
                name: "FK_sys_attach_screen_role_sys_screen_name_screen_uid",
                table: "sys_attach_screen_role",
                column: "screen_uid",
                principalTable: "sys_screen_name",
                principalColumn: "screen_uid",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
