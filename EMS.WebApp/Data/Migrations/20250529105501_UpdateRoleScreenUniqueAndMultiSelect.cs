using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMS.WebApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateRoleScreenUniqueAndMultiSelect : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK__sys_user__B9BE370F9ECB6AC8",
                table: "sys_user");

            migrationBuilder.DropPrimaryKey(
                name: "PK__sys_role__760965CC3DA5ABD8",
                table: "sys_role");

            migrationBuilder.DropPrimaryKey(
                name: "PK__hr_emplo__F9EA96E640CC1C95",
                table: "hr_employee_dependent");

            migrationBuilder.DropPrimaryKey(
                name: "PK__hr_emplo__1299A8610F1C30AD",
                table: "hr_employee");

            migrationBuilder.DropColumn(
                name: "emp_id",
                table: "hr_employee_dependent");

            migrationBuilder.AlterColumn<int>(
                name: "user_id",
                table: "sys_user",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<int>(
                name: "screen_uid",
                table: "sys_screen_name",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<int>(
                name: "role_id",
                table: "sys_role",
                type: "int",
                nullable: false,
                oldClrType: typeof(short),
                oldType: "smallint")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<int>(
                name: "uid",
                table: "sys_attach_screen_role",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<bool>(
                name: "marital_status",
                table: "hr_employee_dependent",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "emp_uid",
                table: "hr_employee_dependent",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "emp_DOB",
                table: "hr_employee",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AddColumn<int>(
                name: "emp_uid",
                table: "hr_employee",
                type: "int",
                nullable: false,
                defaultValue: 0)
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddColumn<string>(
                name: "emp_blood_Group",
                table: "hr_employee",
                type: "varchar(10)",
                unicode: false,
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "plant_id",
                table: "hr_employee",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_sys_user",
                table: "sys_user",
                column: "user_id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_sys_role",
                table: "sys_role",
                column: "role_id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_sys_attach_screen_role",
                table: "sys_attach_screen_role",
                column: "uid");

            migrationBuilder.AddPrimaryKey(
                name: "PK_hr_employee_dependent",
                table: "hr_employee_dependent",
                column: "emp_dep_id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_hr_employee",
                table: "hr_employee",
                column: "emp_uid");

            migrationBuilder.CreateTable(
                name: "account_login",
                columns: table => new
                {
                    login_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    user_name = table.Column<string>(type: "varchar(120)", unicode: false, maxLength: 120, nullable: false),
                    password = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    is_active = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_login", x => x.login_id);
                });

            migrationBuilder.CreateTable(
                name: "med_diagnosis",
                columns: table => new
                {
                    diag_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    diag_name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    diag_desc = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_med_diagnosis", x => x.diag_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sys_user_role_id",
                table: "sys_user",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "IX_sys_attach_screen_role_role_uid",
                table: "sys_attach_screen_role",
                column: "role_uid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sys_attach_screen_role_screen_uid",
                table: "sys_attach_screen_role",
                column: "screen_uid");

            migrationBuilder.CreateIndex(
                name: "IX_hr_employee_dependent_emp_uid",
                table: "hr_employee_dependent",
                column: "emp_uid");

            migrationBuilder.CreateIndex(
                name: "IX_hr_employee_dept_id",
                table: "hr_employee",
                column: "dept_id");

            migrationBuilder.CreateIndex(
                name: "IX_hr_employee_plant_id",
                table: "hr_employee",
                column: "plant_id");

            migrationBuilder.AddForeignKey(
                name: "FK_hr_employee_org_department_dept_id",
                table: "hr_employee",
                column: "dept_id",
                principalTable: "org_department",
                principalColumn: "dept_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_hr_employee_org_plant_plant_id",
                table: "hr_employee",
                column: "plant_id",
                principalTable: "org_plant",
                principalColumn: "plant_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_hr_employee_dependent_hr_employee_emp_uid",
                table: "hr_employee_dependent",
                column: "emp_uid",
                principalTable: "hr_employee",
                principalColumn: "emp_uid",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_sys_attach_screen_role_sys_role_role_uid",
                table: "sys_attach_screen_role",
                column: "role_uid",
                principalTable: "sys_role",
                principalColumn: "role_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_sys_attach_screen_role_sys_screen_name_screen_uid",
                table: "sys_attach_screen_role",
                column: "screen_uid",
                principalTable: "sys_screen_name",
                principalColumn: "screen_uid",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_sys_user_sys_role_role_id",
                table: "sys_user",
                column: "role_id",
                principalTable: "sys_role",
                principalColumn: "role_id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_hr_employee_org_department_dept_id",
                table: "hr_employee");

            migrationBuilder.DropForeignKey(
                name: "FK_hr_employee_org_plant_plant_id",
                table: "hr_employee");

            migrationBuilder.DropForeignKey(
                name: "FK_hr_employee_dependent_hr_employee_emp_uid",
                table: "hr_employee_dependent");

            migrationBuilder.DropForeignKey(
                name: "FK_sys_attach_screen_role_sys_role_role_uid",
                table: "sys_attach_screen_role");

            migrationBuilder.DropForeignKey(
                name: "FK_sys_attach_screen_role_sys_screen_name_screen_uid",
                table: "sys_attach_screen_role");

            migrationBuilder.DropForeignKey(
                name: "FK_sys_user_sys_role_role_id",
                table: "sys_user");

            migrationBuilder.DropTable(
                name: "account_login");

            migrationBuilder.DropTable(
                name: "med_diagnosis");

            migrationBuilder.DropPrimaryKey(
                name: "PK_sys_user",
                table: "sys_user");

            migrationBuilder.DropIndex(
                name: "IX_sys_user_role_id",
                table: "sys_user");

            migrationBuilder.DropPrimaryKey(
                name: "PK_sys_role",
                table: "sys_role");

            migrationBuilder.DropPrimaryKey(
                name: "PK_sys_attach_screen_role",
                table: "sys_attach_screen_role");

            migrationBuilder.DropIndex(
                name: "IX_sys_attach_screen_role_role_uid",
                table: "sys_attach_screen_role");

            migrationBuilder.DropIndex(
                name: "IX_sys_attach_screen_role_screen_uid",
                table: "sys_attach_screen_role");

            migrationBuilder.DropPrimaryKey(
                name: "PK_hr_employee_dependent",
                table: "hr_employee_dependent");

            migrationBuilder.DropIndex(
                name: "IX_hr_employee_dependent_emp_uid",
                table: "hr_employee_dependent");

            migrationBuilder.DropPrimaryKey(
                name: "PK_hr_employee",
                table: "hr_employee");

            migrationBuilder.DropIndex(
                name: "IX_hr_employee_dept_id",
                table: "hr_employee");

            migrationBuilder.DropIndex(
                name: "IX_hr_employee_plant_id",
                table: "hr_employee");

            migrationBuilder.DropColumn(
                name: "emp_uid",
                table: "hr_employee_dependent");

            migrationBuilder.DropColumn(
                name: "emp_uid",
                table: "hr_employee");

            migrationBuilder.DropColumn(
                name: "emp_blood_Group",
                table: "hr_employee");

            migrationBuilder.DropColumn(
                name: "plant_id",
                table: "hr_employee");

            migrationBuilder.AlterColumn<int>(
                name: "user_id",
                table: "sys_user",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<int>(
                name: "screen_uid",
                table: "sys_screen_name",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<short>(
                name: "role_id",
                table: "sys_role",
                type: "smallint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<int>(
                name: "uid",
                table: "sys_attach_screen_role",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<bool>(
                name: "marital_status",
                table: "hr_employee_dependent",
                type: "bit",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AddColumn<string>(
                name: "emp_id",
                table: "hr_employee_dependent",
                type: "varchar(10)",
                unicode: false,
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "emp_DOB",
                table: "hr_employee",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1),
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK__sys_user__B9BE370F9ECB6AC8",
                table: "sys_user",
                column: "user_id");

            migrationBuilder.AddPrimaryKey(
                name: "PK__sys_role__760965CC3DA5ABD8",
                table: "sys_role",
                column: "role_id");

            migrationBuilder.AddPrimaryKey(
                name: "PK__hr_emplo__F9EA96E640CC1C95",
                table: "hr_employee_dependent",
                column: "emp_dep_id");

            migrationBuilder.AddPrimaryKey(
                name: "PK__hr_emplo__1299A8610F1C30AD",
                table: "hr_employee",
                column: "emp_id");
        }
    }
}
