using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMS.WebApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPrescriptionApprovalFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "potency",
                table: "med_master");

            migrationBuilder.DropColumn(
                name: "safe_dose",
                table: "med_master");

            migrationBuilder.AlterColumn<string>(
                name: "hosp_name",
                table: "med_ref_hospital",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "hosp_code",
                table: "med_ref_hospital",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateTable(
                name: "compounder_indent",
                columns: table => new
                {
                    indent_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    indent_type = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    indent_date = table.Column<DateTime>(type: "date", nullable: false),
                    created_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    comments = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: true),
                    approved_by = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    approved_date = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_compounder_indent", x => x.indent_id);
                });

            migrationBuilder.CreateTable(
                name: "med_prescription",
                columns: table => new
                {
                    PrescriptionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    emp_uid = table.Column<int>(type: "int", nullable: false),
                    exam_id = table.Column<int>(type: "int", nullable: false),
                    PrescriptionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BloodPressure = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Pulse = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Temperature = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ApprovalStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Approved"),
                    ApprovedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ApprovedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_med_prescription", x => x.PrescriptionId);
                    table.ForeignKey(
                        name: "FK_med_prescription_hr_employee_emp_uid",
                        column: x => x.emp_uid,
                        principalTable: "hr_employee",
                        principalColumn: "emp_uid");
                    table.ForeignKey(
                        name: "FK_med_prescription_med_exam_header_exam_id",
                        column: x => x.exam_id,
                        principalTable: "med_exam_header",
                        principalColumn: "exam_id");
                });

            migrationBuilder.CreateTable(
                name: "other_patient",
                columns: table => new
                {
                    PatientId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TreatmentId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PatientName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Age = table.Column<int>(type: "int", nullable: true),
                    PNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    OtherDetails = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_other_patient", x => x.PatientId);
                });

            migrationBuilder.CreateTable(
                name: "store_indent",
                columns: table => new
                {
                    indent_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    indent_type = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    indent_date = table.Column<DateTime>(type: "date", nullable: false),
                    created_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    comments = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: true),
                    approved_by = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    approved_date = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_store_indent", x => x.indent_id);
                });

            migrationBuilder.CreateTable(
                name: "compounder_indent_item",
                columns: table => new
                {
                    indent_item_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    indent_id = table.Column<int>(type: "int", nullable: false),
                    med_item_id = table.Column<int>(type: "int", nullable: false),
                    vendor_code = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    raised_quantity = table.Column<int>(type: "int", nullable: false),
                    received_quantity = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    unit_price = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    total_amount = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    batch_no = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    expiry_date = table.Column<DateTime>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_compounder_indent_item", x => x.indent_item_id);
                    table.ForeignKey(
                        name: "FK_compounder_indent_item_compounder_indent_indent_id",
                        column: x => x.indent_id,
                        principalTable: "compounder_indent",
                        principalColumn: "indent_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_compounder_indent_item_med_master_med_item_id",
                        column: x => x.med_item_id,
                        principalTable: "med_master",
                        principalColumn: "med_item_id");
                });

            migrationBuilder.CreateTable(
                name: "med_prescription_disease",
                columns: table => new
                {
                    PrescriptionDiseaseId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PrescriptionId = table.Column<int>(type: "int", nullable: false),
                    DiseaseId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_med_prescription_disease", x => x.PrescriptionDiseaseId);
                    table.ForeignKey(
                        name: "FK_med_prescription_disease_med_disease_DiseaseId",
                        column: x => x.DiseaseId,
                        principalTable: "med_disease",
                        principalColumn: "disease_id");
                    table.ForeignKey(
                        name: "FK_med_prescription_disease_med_prescription_PrescriptionId",
                        column: x => x.PrescriptionId,
                        principalTable: "med_prescription",
                        principalColumn: "PrescriptionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "med_prescription_medicine",
                columns: table => new
                {
                    PrescriptionMedicineId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PrescriptionId = table.Column<int>(type: "int", nullable: false),
                    MedItemId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Dose = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Instructions = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_med_prescription_medicine", x => x.PrescriptionMedicineId);
                    table.ForeignKey(
                        name: "FK_med_prescription_medicine_med_master_MedItemId",
                        column: x => x.MedItemId,
                        principalTable: "med_master",
                        principalColumn: "med_item_id");
                    table.ForeignKey(
                        name: "FK_med_prescription_medicine_med_prescription_PrescriptionId",
                        column: x => x.PrescriptionId,
                        principalTable: "med_prescription",
                        principalColumn: "PrescriptionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "others_diagnosis",
                columns: table => new
                {
                    DiagnosisId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientId = table.Column<int>(type: "int", nullable: false),
                    VisitDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastVisitDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BloodPressure = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PulseRate = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Sugar = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DiagnosedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_others_diagnosis", x => x.DiagnosisId);
                    table.ForeignKey(
                        name: "FK_others_diagnosis_other_patient_PatientId",
                        column: x => x.PatientId,
                        principalTable: "other_patient",
                        principalColumn: "PatientId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "store_indent_item",
                columns: table => new
                {
                    indent_item_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    indent_id = table.Column<int>(type: "int", nullable: false),
                    med_item_id = table.Column<int>(type: "int", nullable: false),
                    vendor_code = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    raised_quantity = table.Column<int>(type: "int", nullable: false),
                    received_quantity = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    unit_price = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    total_amount = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    batch_no = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    expiry_date = table.Column<DateTime>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_store_indent_item", x => x.indent_item_id);
                    table.ForeignKey(
                        name: "FK_store_indent_item_med_master_med_item_id",
                        column: x => x.med_item_id,
                        principalTable: "med_master",
                        principalColumn: "med_item_id");
                    table.ForeignKey(
                        name: "FK_store_indent_item_store_indent_indent_id",
                        column: x => x.indent_id,
                        principalTable: "store_indent",
                        principalColumn: "indent_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "others_diagnosis_disease",
                columns: table => new
                {
                    DiagnosisDiseaseId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DiagnosisId = table.Column<int>(type: "int", nullable: false),
                    DiseaseId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_others_diagnosis_disease", x => x.DiagnosisDiseaseId);
                    table.ForeignKey(
                        name: "FK_others_diagnosis_disease_med_disease_DiseaseId",
                        column: x => x.DiseaseId,
                        principalTable: "med_disease",
                        principalColumn: "disease_id");
                    table.ForeignKey(
                        name: "FK_others_diagnosis_disease_others_diagnosis_DiagnosisId",
                        column: x => x.DiagnosisId,
                        principalTable: "others_diagnosis",
                        principalColumn: "DiagnosisId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "others_diagnosis_medicine",
                columns: table => new
                {
                    DiagnosisMedicineId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DiagnosisId = table.Column<int>(type: "int", nullable: false),
                    MedItemId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Dose = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Instructions = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_others_diagnosis_medicine", x => x.DiagnosisMedicineId);
                    table.ForeignKey(
                        name: "FK_others_diagnosis_medicine_med_master_MedItemId",
                        column: x => x.MedItemId,
                        principalTable: "med_master",
                        principalColumn: "med_item_id");
                    table.ForeignKey(
                        name: "FK_others_diagnosis_medicine_others_diagnosis_DiagnosisId",
                        column: x => x.DiagnosisId,
                        principalTable: "others_diagnosis",
                        principalColumn: "DiagnosisId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrgPlant_PlantCode_Unique",
                table: "org_plant",
                column: "plant_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrgDepartment_DeptName_Unique",
                table: "org_department",
                column: "dept_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MedRefHospital_HospNameCode_Unique",
                table: "med_ref_hospital",
                columns: new[] { "hosp_name", "hosp_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MedMaster_MedItemNameBaseIdCompanyName_Unique",
                table: "med_master",
                columns: new[] { "med_item_name", "base_id", "company_name" },
                unique: true,
                filter: "[base_id] IS NOT NULL AND [company_name] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MedExamCategory_CatNameYearsFreqAnnuallyRuleMonthsSched_Unique",
                table: "med_exam_category",
                columns: new[] { "cat_name", "years_freq", "annually_rule", "months_sched" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MedCategory_MedCatName_Unique",
                table: "med_category",
                column: "medcat_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MedAmbulanceMaster_VehicleNo_Unique",
                table: "med_ambulance_master",
                column: "vehicle_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_compounder_indent_item_med_item_id",
                table: "compounder_indent_item",
                column: "med_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_CompounderIndentItem_IndentIdMedItemId_Unique",
                table: "compounder_indent_item",
                columns: new[] { "indent_id", "med_item_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompounderIndentItem_IndentIdVendorCode_Unique",
                table: "compounder_indent_item",
                columns: new[] { "indent_id", "vendor_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_med_prescription_emp_uid",
                table: "med_prescription",
                column: "emp_uid");

            migrationBuilder.CreateIndex(
                name: "IX_med_prescription_exam_id",
                table: "med_prescription",
                column: "exam_id");

            migrationBuilder.CreateIndex(
                name: "IX_MedPrescription_ApprovalStatus",
                table: "med_prescription",
                column: "ApprovalStatus");

            migrationBuilder.CreateIndex(
                name: "IX_med_prescription_disease_DiseaseId",
                table: "med_prescription_disease",
                column: "DiseaseId");

            migrationBuilder.CreateIndex(
                name: "IX_MedPrescriptionDisease_PrescriptionIdDiseaseId_Unique",
                table: "med_prescription_disease",
                columns: new[] { "PrescriptionId", "DiseaseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_med_prescription_medicine_MedItemId",
                table: "med_prescription_medicine",
                column: "MedItemId");

            migrationBuilder.CreateIndex(
                name: "IX_MedPrescriptionMedicine_PrescriptionIdMedItemId_Unique",
                table: "med_prescription_medicine",
                columns: new[] { "PrescriptionId", "MedItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OtherPatient_TreatmentId_Unique",
                table: "other_patient",
                column: "TreatmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_others_diagnosis_PatientId",
                table: "others_diagnosis",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_others_diagnosis_disease_DiseaseId",
                table: "others_diagnosis_disease",
                column: "DiseaseId");

            migrationBuilder.CreateIndex(
                name: "IX_OthersDiagnosisDisease_DiagnosisIdDiseaseId_Unique",
                table: "others_diagnosis_disease",
                columns: new[] { "DiagnosisId", "DiseaseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_others_diagnosis_medicine_MedItemId",
                table: "others_diagnosis_medicine",
                column: "MedItemId");

            migrationBuilder.CreateIndex(
                name: "IX_OthersDiagnosisMedicine_DiagnosisIdMedItemId_Unique",
                table: "others_diagnosis_medicine",
                columns: new[] { "DiagnosisId", "MedItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_store_indent_item_med_item_id",
                table: "store_indent_item",
                column: "med_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_StoreIndentItem_IndentIdMedItemId_Unique",
                table: "store_indent_item",
                columns: new[] { "indent_id", "med_item_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StoreIndentItem_IndentIdVendorCode_Unique",
                table: "store_indent_item",
                columns: new[] { "indent_id", "vendor_code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "compounder_indent_item");

            migrationBuilder.DropTable(
                name: "med_prescription_disease");

            migrationBuilder.DropTable(
                name: "med_prescription_medicine");

            migrationBuilder.DropTable(
                name: "others_diagnosis_disease");

            migrationBuilder.DropTable(
                name: "others_diagnosis_medicine");

            migrationBuilder.DropTable(
                name: "store_indent_item");

            migrationBuilder.DropTable(
                name: "compounder_indent");

            migrationBuilder.DropTable(
                name: "med_prescription");

            migrationBuilder.DropTable(
                name: "others_diagnosis");

            migrationBuilder.DropTable(
                name: "store_indent");

            migrationBuilder.DropTable(
                name: "other_patient");

            migrationBuilder.DropIndex(
                name: "IX_OrgPlant_PlantCode_Unique",
                table: "org_plant");

            migrationBuilder.DropIndex(
                name: "IX_OrgDepartment_DeptName_Unique",
                table: "org_department");

            migrationBuilder.DropIndex(
                name: "IX_MedRefHospital_HospNameCode_Unique",
                table: "med_ref_hospital");

            migrationBuilder.DropIndex(
                name: "IX_MedMaster_MedItemNameBaseIdCompanyName_Unique",
                table: "med_master");

            migrationBuilder.DropIndex(
                name: "IX_MedExamCategory_CatNameYearsFreqAnnuallyRuleMonthsSched_Unique",
                table: "med_exam_category");

            migrationBuilder.DropIndex(
                name: "IX_MedCategory_MedCatName_Unique",
                table: "med_category");

            migrationBuilder.DropIndex(
                name: "IX_MedAmbulanceMaster_VehicleNo_Unique",
                table: "med_ambulance_master");

            migrationBuilder.AlterColumn<string>(
                name: "hosp_name",
                table: "med_ref_hospital",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "hosp_code",
                table: "med_ref_hospital",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AddColumn<string>(
                name: "potency",
                table: "med_master",
                type: "varchar(40)",
                unicode: false,
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "safe_dose",
                table: "med_master",
                type: "int",
                nullable: true);
        }
    }
}
