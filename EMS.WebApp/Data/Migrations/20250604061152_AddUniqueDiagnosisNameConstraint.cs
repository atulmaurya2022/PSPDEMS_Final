using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMS.WebApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueDiagnosisNameConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_action");

            migrationBuilder.AddColumn<string>(
                name: "SessionToken",
                table: "account_login",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TokenIssuedAt",
                table: "account_login",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SysRole_RoleName_Unique",
                table: "sys_role",
                column: "role_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MedDiagnosis_DiagName_Unique",
                table: "med_diagnosis",
                column: "diag_name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SysRole_RoleName_Unique",
                table: "sys_role");

            migrationBuilder.DropIndex(
                name: "IX_MedDiagnosis_DiagName_Unique",
                table: "med_diagnosis");

            migrationBuilder.DropColumn(
                name: "SessionToken",
                table: "account_login");

            migrationBuilder.DropColumn(
                name: "TokenIssuedAt",
                table: "account_login");

            migrationBuilder.CreateTable(
                name: "user_action",
                columns: table => new
                {
                    uid = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    action_type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    time_stamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    user_id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_action", x => x.uid);
                });
        }
    }
}
