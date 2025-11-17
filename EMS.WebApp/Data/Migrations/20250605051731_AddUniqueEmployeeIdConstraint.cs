using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMS.WebApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueEmployeeIdConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "med_exam_header",
                columns: table => new
                {
                    exam_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    emp_uid = table.Column<int>(type: "int", nullable: false),
                    exam_date = table.Column<DateOnly>(type: "date", nullable: true),
                    food_habit = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_med_exam_header", x => x.exam_id);
                    table.ForeignKey(
                        name: "FK_med_exam_header_hr_employee_emp_uid",
                        column: x => x.emp_uid,
                        principalTable: "hr_employee",
                        principalColumn: "emp_uid",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ref_med_condition",
                columns: table => new
                {
                    cond_uid = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    cond_code = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: true),
                    cond_desc = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ref_med_condition", x => x.cond_uid);
                });

            migrationBuilder.CreateTable(
                name: "ref_work_area",
                columns: table => new
                {
                    area_uid = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    area_code = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: true),
                    area_desc = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ref_work_area", x => x.area_uid);
                });

            migrationBuilder.CreateTable(
                name: "med_general_exam",
                columns: table => new
                {
                    general_exam_uid = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    emp_uid = table.Column<int>(type: "int", nullable: false),
                    exam_id = table.Column<int>(type: "int", nullable: false),
                    height_cm = table.Column<short>(type: "smallint", nullable: true),
                    weight_kg = table.Column<short>(type: "smallint", nullable: true),
                    abdomen = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: true),
                    pulse = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    bp = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    bmi = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    ent = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    rr = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    opthal = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    cvs = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    skin = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    cns = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    genito_urinary = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    respiratory = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    others = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: true),
                    remarks = table.Column<string>(type: "varchar(2000)", unicode: false, maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_med_general_exam", x => x.general_exam_uid);
                    table.ForeignKey(
                        name: "FK_med_general_exam_hr_employee_emp_uid",
                        column: x => x.emp_uid,
                        principalTable: "hr_employee",
                        principalColumn: "emp_uid",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_med_general_exam_med_exam_header_exam_id",
                        column: x => x.exam_id,
                        principalTable: "med_exam_header",
                        principalColumn: "exam_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "med_work_history",
                columns: table => new
                {
                    work_uid = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    emp_uid = table.Column<int>(type: "int", nullable: false),
                    exam_id = table.Column<int>(type: "int", nullable: false),
                    job_name = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: true),
                    years_in_job = table.Column<decimal>(type: "decimal(4,1)", nullable: true),
                    work_env = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: true),
                    ppe = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: true),
                    job_injuries = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_med_work_history", x => x.work_uid);
                    table.ForeignKey(
                        name: "FK_med_work_history_hr_employee_emp_uid",
                        column: x => x.emp_uid,
                        principalTable: "hr_employee",
                        principalColumn: "emp_uid",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_med_work_history_med_exam_header_exam_id",
                        column: x => x.exam_id,
                        principalTable: "med_exam_header",
                        principalColumn: "exam_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "med_exam_condition",
                columns: table => new
                {
                    exam_condition_uid = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    exam_id = table.Column<int>(type: "int", nullable: false),
                    cond_uid = table.Column<int>(type: "int", nullable: false),
                    present = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_med_exam_condition", x => x.exam_condition_uid);
                    table.ForeignKey(
                        name: "FK_med_exam_condition_med_exam_header_exam_id",
                        column: x => x.exam_id,
                        principalTable: "med_exam_header",
                        principalColumn: "exam_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_med_exam_condition_ref_med_condition_cond_uid",
                        column: x => x.cond_uid,
                        principalTable: "ref_med_condition",
                        principalColumn: "cond_uid",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "med_exam_work_area",
                columns: table => new
                {
                    work_area_uid = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    exam_id = table.Column<int>(type: "int", nullable: false),
                    area_uid = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_med_exam_work_area", x => x.work_area_uid);
                    table.ForeignKey(
                        name: "FK_med_exam_work_area_med_exam_header_exam_id",
                        column: x => x.exam_id,
                        principalTable: "med_exam_header",
                        principalColumn: "exam_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_med_exam_work_area_ref_work_area_area_uid",
                        column: x => x.area_uid,
                        principalTable: "ref_work_area",
                        principalColumn: "area_uid",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HrEmployee_EmpId_Unique",
                table: "hr_employee",
                column: "emp_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_med_exam_condition_cond_uid",
                table: "med_exam_condition",
                column: "cond_uid");

            migrationBuilder.CreateIndex(
                name: "IX_med_exam_condition_exam_id",
                table: "med_exam_condition",
                column: "exam_id");

            migrationBuilder.CreateIndex(
                name: "IX_med_exam_header_emp_uid",
                table: "med_exam_header",
                column: "emp_uid");

            migrationBuilder.CreateIndex(
                name: "IX_med_exam_work_area_area_uid",
                table: "med_exam_work_area",
                column: "area_uid");

            migrationBuilder.CreateIndex(
                name: "IX_med_exam_work_area_exam_id",
                table: "med_exam_work_area",
                column: "exam_id");

            migrationBuilder.CreateIndex(
                name: "IX_med_general_exam_emp_uid",
                table: "med_general_exam",
                column: "emp_uid");

            migrationBuilder.CreateIndex(
                name: "IX_med_general_exam_exam_id",
                table: "med_general_exam",
                column: "exam_id");

            migrationBuilder.CreateIndex(
                name: "IX_med_work_history_emp_uid",
                table: "med_work_history",
                column: "emp_uid");

            migrationBuilder.CreateIndex(
                name: "IX_med_work_history_exam_id",
                table: "med_work_history",
                column: "exam_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "med_exam_condition");

            migrationBuilder.DropTable(
                name: "med_exam_work_area");

            migrationBuilder.DropTable(
                name: "med_general_exam");

            migrationBuilder.DropTable(
                name: "med_work_history");

            migrationBuilder.DropTable(
                name: "ref_med_condition");

            migrationBuilder.DropTable(
                name: "ref_work_area");

            migrationBuilder.DropTable(
                name: "med_exam_header");

            migrationBuilder.DropIndex(
                name: "IX_HrEmployee_EmpId_Unique",
                table: "hr_employee");
        }
    }
}
