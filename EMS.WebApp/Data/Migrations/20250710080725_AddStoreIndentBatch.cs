using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMS.WebApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStoreIndentBatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_med_master_med_base_base_id",
                table: "med_master");

            migrationBuilder.DropIndex(
                name: "IX_MedMaster_MedItemNameBaseIdCompanyName_Unique",
                table: "med_master");

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "sys_user",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_on",
                table: "sys_user",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modified_by",
                table: "sys_user",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "modified_on",
                table: "sys_user",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "sys_screen_name",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_on",
                table: "sys_screen_name",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modified_by",
                table: "sys_screen_name",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "modified_on",
                table: "sys_screen_name",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "sys_role",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_on",
                table: "sys_role",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modified_by",
                table: "sys_role",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "modified_on",
                table: "sys_role",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "sys_attach_screen_role",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_on",
                table: "sys_attach_screen_role",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modified_by",
                table: "sys_attach_screen_role",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "modified_on",
                table: "sys_attach_screen_role",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "approval_status",
                table: "others_diagnosis",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Approved");

            migrationBuilder.AddColumn<string>(
                name: "approved_by",
                table: "others_diagnosis",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "approved_date",
                table: "others_diagnosis",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rejection_reason",
                table: "others_diagnosis",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "visit_type",
                table: "others_diagnosis",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Regular Visitor");

            migrationBuilder.AlterColumn<decimal>(
                name: "Age",
                table: "other_patient",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "org_plant",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_on",
                table: "org_plant",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modified_by",
                table: "org_plant",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "modified_on",
                table: "org_plant",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "org_department",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_on",
                table: "org_department",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modified_by",
                table: "org_department",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "modified_on",
                table: "org_department",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "vendor_name",
                table: "med_ref_hospital",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "vendor_code",
                table: "med_ref_hospital",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "speciality",
                table: "med_ref_hospital",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "hosp_name",
                table: "med_ref_hospital",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "hosp_code",
                table: "med_ref_hospital",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "description",
                table: "med_ref_hospital",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "contact_person_name",
                table: "med_ref_hospital",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "contact_person_email_id",
                table: "med_ref_hospital",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "address",
                table: "med_ref_hospital",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "med_ref_hospital",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_on",
                table: "med_ref_hospital",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modified_by",
                table: "med_ref_hospital",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "modified_on",
                table: "med_ref_hospital",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "med_prescription_disease",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedDate",
                table: "med_prescription_disease",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AlterColumn<int>(
                name: "base_id",
                table: "med_master",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "med_master",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_on",
                table: "med_master",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modified_by",
                table: "med_master",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "modified_on",
                table: "med_master",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "med_exam_category",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_on",
                table: "med_exam_category",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modified_by",
                table: "med_exam_category",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "modified_on",
                table: "med_exam_category",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "med_disease",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_on",
                table: "med_disease",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modified_by",
                table: "med_disease",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "modified_on",
                table: "med_disease",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "med_diagnosis",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_on",
                table: "med_diagnosis",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modified_by",
                table: "med_diagnosis",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "modified_on",
                table: "med_diagnosis",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "med_category",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_on",
                table: "med_category",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modified_by",
                table: "med_category",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "modified_on",
                table: "med_category",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "med_base",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_on",
                table: "med_base",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modified_by",
                table: "med_base",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "modified_on",
                table: "med_base",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "med_ambulance_master",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_on",
                table: "med_ambulance_master",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modified_by",
                table: "med_ambulance_master",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "modified_on",
                table: "med_ambulance_master",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "hr_employee_dependent",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_on",
                table: "hr_employee_dependent",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modified_by",
                table: "hr_employee_dependent",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "modified_on",
                table: "hr_employee_dependent",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "hr_employee",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_on",
                table: "hr_employee",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modified_by",
                table: "hr_employee",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "modified_on",
                table: "hr_employee",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "available_stock",
                table: "compounder_indent_item",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "expired_medicine",
                columns: table => new
                {
                    expired_medicine_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    compounder_indent_item_id = table.Column<int>(type: "int", nullable: false),
                    medicine_name = table.Column<string>(type: "varchar(120)", unicode: false, maxLength: 120, nullable: false),
                    company_name = table.Column<string>(type: "varchar(120)", unicode: false, maxLength: 120, nullable: true),
                    batch_number = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    vendor_code = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    expiry_date = table.Column<DateTime>(type: "date", nullable: false),
                    quantity_expired = table.Column<int>(type: "int", nullable: false),
                    indent_id = table.Column<int>(type: "int", nullable: false),
                    indent_number = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    unit_price = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    total_value = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    detected_date = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    detected_by = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    status = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false, defaultValue: "Pending Disposal"),
                    biomedical_waste_issued_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    biomedical_waste_issued_by = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    type_of_medicine = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false, defaultValue: "Select Type of Medicine")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expired_medicine", x => x.expired_medicine_id);
                    table.ForeignKey(
                        name: "FK_expired_medicine_compounder_indent_item_compounder_indent_item_id",
                        column: x => x.compounder_indent_item_id,
                        principalTable: "compounder_indent_item",
                        principalColumn: "indent_item_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "store_indent_batch",
                columns: table => new
                {
                    batch_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    indent_item_id = table.Column<int>(type: "int", nullable: false),
                    batch_no = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    expiry_date = table.Column<DateTime>(type: "date", nullable: false),
                    received_quantity = table.Column<int>(type: "int", nullable: false),
                    vendor_code = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_store_indent_batch", x => x.batch_id);
                    table.ForeignKey(
                        name: "FK_store_indent_batch_store_indent_item_indent_item_id",
                        column: x => x.indent_item_id,
                        principalTable: "store_indent_item",
                        principalColumn: "indent_item_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OthersDiagnosis_ApprovalStatus",
                table: "others_diagnosis",
                column: "approval_status");

            migrationBuilder.CreateIndex(
                name: "IX_OthersDiagnosis_ApprovalStatus_VisitType",
                table: "others_diagnosis",
                columns: new[] { "approval_status", "visit_type" });

            migrationBuilder.CreateIndex(
                name: "IX_OthersDiagnosis_VisitType",
                table: "others_diagnosis",
                column: "visit_type");

            migrationBuilder.CreateIndex(
                name: "IX_MedMaster_MedItemNameBaseIdCompanyName_Unique",
                table: "med_master",
                columns: new[] { "med_item_name", "base_id", "company_name" },
                unique: true,
                filter: "[company_name] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ExpiredMedicine_BatchNumber",
                table: "expired_medicine",
                column: "batch_number");

            migrationBuilder.CreateIndex(
                name: "IX_ExpiredMedicine_DetectedDate",
                table: "expired_medicine",
                column: "detected_date");

            migrationBuilder.CreateIndex(
                name: "IX_ExpiredMedicine_ExpiryDate",
                table: "expired_medicine",
                column: "expiry_date");

            migrationBuilder.CreateIndex(
                name: "IX_ExpiredMedicine_MedicineName",
                table: "expired_medicine",
                column: "medicine_name");

            migrationBuilder.CreateIndex(
                name: "IX_ExpiredMedicine_Status",
                table: "expired_medicine",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_ExpiredMedicine_Status_ExpiryDate",
                table: "expired_medicine",
                columns: new[] { "status", "expiry_date" });

            migrationBuilder.CreateIndex(
                name: "IX_ExpiredMedicine_TypeOfMedicine",
                table: "expired_medicine",
                column: "type_of_medicine");

            migrationBuilder.CreateIndex(
                name: "IX_ExpiredMedicine_VendorCode",
                table: "expired_medicine",
                column: "vendor_code");

            migrationBuilder.CreateIndex(
                name: "UK_ExpiredMedicine_CompounderIndentItem",
                table: "expired_medicine",
                column: "compounder_indent_item_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_store_indent_batch_indent_item_id",
                table: "store_indent_batch",
                column: "indent_item_id");

            migrationBuilder.AddForeignKey(
                name: "FK_med_master_med_base_base_id",
                table: "med_master",
                column: "base_id",
                principalTable: "med_base",
                principalColumn: "base_id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_med_master_med_base_base_id",
                table: "med_master");

            migrationBuilder.DropTable(
                name: "expired_medicine");

            migrationBuilder.DropTable(
                name: "store_indent_batch");

            migrationBuilder.DropIndex(
                name: "IX_OthersDiagnosis_ApprovalStatus",
                table: "others_diagnosis");

            migrationBuilder.DropIndex(
                name: "IX_OthersDiagnosis_ApprovalStatus_VisitType",
                table: "others_diagnosis");

            migrationBuilder.DropIndex(
                name: "IX_OthersDiagnosis_VisitType",
                table: "others_diagnosis");

            migrationBuilder.DropIndex(
                name: "IX_MedMaster_MedItemNameBaseIdCompanyName_Unique",
                table: "med_master");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "sys_user");

            migrationBuilder.DropColumn(
                name: "created_on",
                table: "sys_user");

            migrationBuilder.DropColumn(
                name: "modified_by",
                table: "sys_user");

            migrationBuilder.DropColumn(
                name: "modified_on",
                table: "sys_user");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "sys_screen_name");

            migrationBuilder.DropColumn(
                name: "created_on",
                table: "sys_screen_name");

            migrationBuilder.DropColumn(
                name: "modified_by",
                table: "sys_screen_name");

            migrationBuilder.DropColumn(
                name: "modified_on",
                table: "sys_screen_name");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "sys_role");

            migrationBuilder.DropColumn(
                name: "created_on",
                table: "sys_role");

            migrationBuilder.DropColumn(
                name: "modified_by",
                table: "sys_role");

            migrationBuilder.DropColumn(
                name: "modified_on",
                table: "sys_role");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "sys_attach_screen_role");

            migrationBuilder.DropColumn(
                name: "created_on",
                table: "sys_attach_screen_role");

            migrationBuilder.DropColumn(
                name: "modified_by",
                table: "sys_attach_screen_role");

            migrationBuilder.DropColumn(
                name: "modified_on",
                table: "sys_attach_screen_role");

            migrationBuilder.DropColumn(
                name: "approval_status",
                table: "others_diagnosis");

            migrationBuilder.DropColumn(
                name: "approved_by",
                table: "others_diagnosis");

            migrationBuilder.DropColumn(
                name: "approved_date",
                table: "others_diagnosis");

            migrationBuilder.DropColumn(
                name: "rejection_reason",
                table: "others_diagnosis");

            migrationBuilder.DropColumn(
                name: "visit_type",
                table: "others_diagnosis");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "org_plant");

            migrationBuilder.DropColumn(
                name: "created_on",
                table: "org_plant");

            migrationBuilder.DropColumn(
                name: "modified_by",
                table: "org_plant");

            migrationBuilder.DropColumn(
                name: "modified_on",
                table: "org_plant");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "org_department");

            migrationBuilder.DropColumn(
                name: "created_on",
                table: "org_department");

            migrationBuilder.DropColumn(
                name: "modified_by",
                table: "org_department");

            migrationBuilder.DropColumn(
                name: "modified_on",
                table: "org_department");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "med_ref_hospital");

            migrationBuilder.DropColumn(
                name: "created_on",
                table: "med_ref_hospital");

            migrationBuilder.DropColumn(
                name: "modified_by",
                table: "med_ref_hospital");

            migrationBuilder.DropColumn(
                name: "modified_on",
                table: "med_ref_hospital");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "med_prescription_disease");

            migrationBuilder.DropColumn(
                name: "CreatedDate",
                table: "med_prescription_disease");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "med_master");

            migrationBuilder.DropColumn(
                name: "created_on",
                table: "med_master");

            migrationBuilder.DropColumn(
                name: "modified_by",
                table: "med_master");

            migrationBuilder.DropColumn(
                name: "modified_on",
                table: "med_master");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "med_exam_category");

            migrationBuilder.DropColumn(
                name: "created_on",
                table: "med_exam_category");

            migrationBuilder.DropColumn(
                name: "modified_by",
                table: "med_exam_category");

            migrationBuilder.DropColumn(
                name: "modified_on",
                table: "med_exam_category");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "med_disease");

            migrationBuilder.DropColumn(
                name: "created_on",
                table: "med_disease");

            migrationBuilder.DropColumn(
                name: "modified_by",
                table: "med_disease");

            migrationBuilder.DropColumn(
                name: "modified_on",
                table: "med_disease");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "med_diagnosis");

            migrationBuilder.DropColumn(
                name: "created_on",
                table: "med_diagnosis");

            migrationBuilder.DropColumn(
                name: "modified_by",
                table: "med_diagnosis");

            migrationBuilder.DropColumn(
                name: "modified_on",
                table: "med_diagnosis");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "med_category");

            migrationBuilder.DropColumn(
                name: "created_on",
                table: "med_category");

            migrationBuilder.DropColumn(
                name: "modified_by",
                table: "med_category");

            migrationBuilder.DropColumn(
                name: "modified_on",
                table: "med_category");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "med_base");

            migrationBuilder.DropColumn(
                name: "created_on",
                table: "med_base");

            migrationBuilder.DropColumn(
                name: "modified_by",
                table: "med_base");

            migrationBuilder.DropColumn(
                name: "modified_on",
                table: "med_base");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "med_ambulance_master");

            migrationBuilder.DropColumn(
                name: "created_on",
                table: "med_ambulance_master");

            migrationBuilder.DropColumn(
                name: "modified_by",
                table: "med_ambulance_master");

            migrationBuilder.DropColumn(
                name: "modified_on",
                table: "med_ambulance_master");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "hr_employee_dependent");

            migrationBuilder.DropColumn(
                name: "created_on",
                table: "hr_employee_dependent");

            migrationBuilder.DropColumn(
                name: "modified_by",
                table: "hr_employee_dependent");

            migrationBuilder.DropColumn(
                name: "modified_on",
                table: "hr_employee_dependent");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "hr_employee");

            migrationBuilder.DropColumn(
                name: "created_on",
                table: "hr_employee");

            migrationBuilder.DropColumn(
                name: "modified_by",
                table: "hr_employee");

            migrationBuilder.DropColumn(
                name: "modified_on",
                table: "hr_employee");

            migrationBuilder.DropColumn(
                name: "available_stock",
                table: "compounder_indent_item");

            migrationBuilder.AlterColumn<int>(
                name: "Age",
                table: "other_patient",
                type: "int",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<string>(
                name: "vendor_name",
                table: "med_ref_hospital",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(120)",
                oldMaxLength: 120);

            migrationBuilder.AlterColumn<string>(
                name: "vendor_code",
                table: "med_ref_hospital",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "speciality",
                table: "med_ref_hospital",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "hosp_name",
                table: "med_ref_hospital",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(120)",
                oldMaxLength: 120);

            migrationBuilder.AlterColumn<string>(
                name: "hosp_code",
                table: "med_ref_hospital",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "description",
                table: "med_ref_hospital",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "contact_person_name",
                table: "med_ref_hospital",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "contact_person_email_id",
                table: "med_ref_hospital",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "address",
                table: "med_ref_hospital",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<int>(
                name: "base_id",
                table: "med_master",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateIndex(
                name: "IX_MedMaster_MedItemNameBaseIdCompanyName_Unique",
                table: "med_master",
                columns: new[] { "med_item_name", "base_id", "company_name" },
                unique: true,
                filter: "[base_id] IS NOT NULL AND [company_name] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_med_master_med_base_base_id",
                table: "med_master",
                column: "base_id",
                principalTable: "med_base",
                principalColumn: "base_id");
        }
    }
}
