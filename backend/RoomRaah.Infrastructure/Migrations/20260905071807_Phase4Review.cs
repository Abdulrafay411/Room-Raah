using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoomRaah.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase4Review : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PropertyRevisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PropertyId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReviewStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AreaId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    AddressLine = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Latitude = table.Column<double>(type: "float", nullable: false),
                    Longitude = table.Column<double>(type: "float", nullable: false),
                    RoomType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    GenderPolicy = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TotalBeds = table.Column<int>(type: "int", nullable: false),
                    AvailableBeds = table.Column<int>(type: "int", nullable: false),
                    MonthlyRent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SecurityDeposit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UtilitiesCharge = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MessCharge = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    HouseRules = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    FacilityIds = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    PhotoOrder = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    PrimaryPhotoId = table.Column<int>(type: "int", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedByAdminId = table.Column<int>(type: "int", nullable: true),
                    AdminNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropertyRevisions_AppUsers_ReviewedByAdminId",
                        column: x => x.ReviewedByAdminId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PropertyRevisions_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VerificationChecks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PropertyId = table.Column<int>(type: "int", nullable: false),
                    CheckType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EvidenceDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedByAdminId = table.Column<int>(type: "int", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VerificationChecks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VerificationChecks_AppUsers_ReviewedByAdminId",
                        column: x => x.ReviewedByAdminId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VerificationChecks_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PropertyRevisions_PropertyId_ReviewStatus_Kind",
                table: "PropertyRevisions",
                columns: new[] { "PropertyId", "ReviewStatus", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_PropertyRevisions_ReviewedByAdminId",
                table: "PropertyRevisions",
                column: "ReviewedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyRevisions_ReviewStatus_Kind_SubmittedAt",
                table: "PropertyRevisions",
                columns: new[] { "ReviewStatus", "Kind", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_VerificationChecks_PropertyId_CheckType",
                table: "VerificationChecks",
                columns: new[] { "PropertyId", "CheckType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VerificationChecks_ReviewedByAdminId",
                table: "VerificationChecks",
                column: "ReviewedByAdminId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PropertyRevisions");

            migrationBuilder.DropTable(
                name: "VerificationChecks");
        }
    }
}
