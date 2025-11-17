using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMS.WebApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixedMedBaseRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.CreateTable(
                name: "hr_employee",
                columns: table => new
                {
                    emp_id = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    emp_name = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    emp_DOB = table.Column<DateOnly>(type: "date", nullable: false),
                    emp_Gender = table.Column<string>(type: "char(1)", unicode: false, fixedLength: true, maxLength: 1, nullable: false),
                    emp_Grade = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    dept_id = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__hr_emplo__1299A8610F1C30AD", x => x.emp_id);
                });

            migrationBuilder.CreateTable(
                name: "hr_employee_dependent",
                columns: table => new
                {
                    emp_dep_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    emp_id = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    dep_name = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: false),
                    dep_dob = table.Column<DateOnly>(type: "date", nullable: true),
                    relation = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    gender = table.Column<string>(type: "char(1)", unicode: false, fixedLength: true, maxLength: 1, nullable: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    marital_status = table.Column<bool>(type: "bit", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__hr_emplo__F9EA96E640CC1C95", x => x.emp_dep_id);
                });

            migrationBuilder.CreateTable(
                name: "med_ambulance_master",
                columns: table => new
                {
                    amb_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    vehicle_no = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    provider = table.Column<string>(type: "varchar(120)", unicode: false, maxLength: 120, nullable: false),
                    vehicle_type = table.Column<string>(type: "varchar(15)", unicode: false, maxLength: 15, nullable: true),
                    max_capacity = table.Column<byte>(type: "tinyint", nullable: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__med_ambu__9FDA4AE611BCF6CF", x => x.amb_id);
                });

            migrationBuilder.CreateTable(
                name: "med_base",
                columns: table => new
                {
                    base_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    base_name = table.Column<string>(type: "varchar(120)", unicode: false, maxLength: 120, nullable: false),
                    base_desc = table.Column<string>(type: "varchar(120)", unicode: false, maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_med_base", x => x.base_id);
                });

            migrationBuilder.CreateTable(
                name: "med_category",
                columns: table => new
                {
                    medcat_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    medcat_name = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: false),
                    description = table.Column<string>(type: "varchar(120)", unicode: false, maxLength: 120, nullable: true),
                    remarks = table.Column<string>(type: "varchar(120)", unicode: false, maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_med_category", x => x.medcat_id);
                });

            migrationBuilder.CreateTable(
                name: "med_disease",
                columns: table => new
                {
                    disease_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    disease_name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    disease_desc = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_med_disease", x => x.disease_id);
                });

            migrationBuilder.CreateTable(
                name: "med_exam_category",
                columns: table => new
                {
                    cat_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    cat_name = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    years_freq = table.Column<byte>(type: "tinyint", nullable: false),
                    annually_rule = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    months_sched = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    remarks = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_med_exam_category", x => x.cat_id);
                });

            migrationBuilder.CreateTable(
                name: "med_ref_hospital",
                columns: table => new
                {
                    hosp_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    hosp_name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    hosp_code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    speciality = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    address = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    tax_category = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    vendor_name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    vendor_code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    contact_person_name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    contact_person_email_id = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    mobile_number_1 = table.Column<long>(type: "bigint", nullable: true),
                    mobile_number_2 = table.Column<long>(type: "bigint", nullable: true),
                    phone_number_1 = table.Column<long>(type: "bigint", nullable: true),
                    phone_number_2 = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_med_ref_hospital", x => x.hosp_id);
                });

            migrationBuilder.CreateTable(
                name: "org_department",
                columns: table => new
                {
                    dept_id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    dept_name = table.Column<string>(type: "varchar(25)", unicode: false, maxLength: 25, nullable: false),
                    dept_description = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    Remarks = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__org_depa__DCA65974896CEDB8", x => x.dept_id);
                });

            migrationBuilder.CreateTable(
                name: "org_plant",
                columns: table => new
                {
                    plant_id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    plant_code = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    plant_name = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__org_plan__A576B3B47C5A3448", x => x.plant_id);
                });

            migrationBuilder.CreateTable(
                name: "sys_attach_screen_role",
                columns: table => new
                {
                    uid = table.Column<int>(type: "int", nullable: false),
                    role_uid = table.Column<int>(type: "int", nullable: false),
                    screen_uid = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "sys_role",
                columns: table => new
                {
                    role_id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    role_name = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    role_desc = table.Column<string>(type: "varchar(120)", unicode: false, maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__sys_role__760965CC3DA5ABD8", x => x.role_id);
                });

            migrationBuilder.CreateTable(
                name: "sys_screen_name",
                columns: table => new
                {
                    screen_uid = table.Column<int>(type: "int", nullable: false),
                    screen_name = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    screen_description = table.Column<string>(type: "varchar(120)", unicode: false, maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__sys_scre__B2C9B83A098D0056", x => x.screen_uid);
                });

            migrationBuilder.CreateTable(
                name: "sys_user",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "int", nullable: false),
                    role_id = table.Column<int>(type: "int", nullable: false),
                    full_name = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: false),
                    email = table.Column<string>(type: "varchar(120)", unicode: false, maxLength: 120, nullable: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__sys_user__B9BE370F9ECB6AC8", x => x.user_id);
                });

            migrationBuilder.CreateTable(
                name: "med_master",
                columns: table => new
                {
                    med_item_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    med_item_name = table.Column<string>(type: "varchar(120)", unicode: false, maxLength: 120, nullable: false),
                    base_id = table.Column<int>(type: "int", nullable: true),
                    safe_dose = table.Column<int>(type: "int", nullable: true),
                    company_name = table.Column<string>(type: "varchar(120)", unicode: false, maxLength: 120, nullable: true),
                    potency = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: true),
                    reorder_limit = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_med_master", x => x.med_item_id);
                    table.ForeignKey(
                        name: "FK_med_master_med_base_base_id",
                        column: x => x.base_id,
                        principalTable: "med_base",
                        principalColumn: "base_id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_med_master_base_id",
                table: "med_master",
                column: "base_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "hr_employee");

            migrationBuilder.DropTable(
                name: "hr_employee_dependent");

            migrationBuilder.DropTable(
                name: "med_ambulance_master");

            migrationBuilder.DropTable(
                name: "med_category");

            migrationBuilder.DropTable(
                name: "med_disease");

            migrationBuilder.DropTable(
                name: "med_exam_category");

            migrationBuilder.DropTable(
                name: "med_master");

            migrationBuilder.DropTable(
                name: "med_ref_hospital");

            migrationBuilder.DropTable(
                name: "org_department");

            migrationBuilder.DropTable(
                name: "org_plant");

            migrationBuilder.DropTable(
                name: "sys_attach_screen_role");

            migrationBuilder.DropTable(
                name: "sys_role");

            migrationBuilder.DropTable(
                name: "sys_screen_name");

            migrationBuilder.DropTable(
                name: "sys_user");

            migrationBuilder.DropTable(
                name: "med_base");

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");
        }
    }
}
