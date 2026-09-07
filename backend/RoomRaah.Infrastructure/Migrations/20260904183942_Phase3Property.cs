using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoomRaah.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase3Property : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Properties",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnerId = table.Column<int>(type: "int", nullable: false),
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
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    HasInspectionBadge = table.Column<bool>(type: "bit", nullable: false),
                    LastInspectionAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AvailabilityConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Properties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Properties_AppUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Properties_Areas_AreaId",
                        column: x => x.AreaId,
                        principalTable: "Areas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PropertyFacilities",
                columns: table => new
                {
                    PropertyId = table.Column<int>(type: "int", nullable: false),
                    FacilityId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyFacilities", x => new { x.PropertyId, x.FacilityId });
                    table.ForeignKey(
                        name: "FK_PropertyFacilities_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PropertyFacilities_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PropertyPhotos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PropertyId = table.Column<int>(type: "int", nullable: false),
                    BlobUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ThumbnailUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyPhotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropertyPhotos_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Properties_AreaId",
                table: "Properties",
                column: "AreaId");

            migrationBuilder.CreateIndex(
                name: "IX_Properties_AvailabilityConfirmedAt",
                table: "Properties",
                column: "AvailabilityConfirmedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Properties_Latitude_Longitude",
                table: "Properties",
                columns: new[] { "Latitude", "Longitude" });

            migrationBuilder.CreateIndex(
                name: "IX_Properties_OwnerId",
                table: "Properties",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Properties_Status_AreaId",
                table: "Properties",
                columns: new[] { "Status", "AreaId" });

            migrationBuilder.CreateIndex(
                name: "IX_Properties_Status_MonthlyRent",
                table: "Properties",
                columns: new[] { "Status", "MonthlyRent" });

            migrationBuilder.CreateIndex(
                name: "IX_PropertyFacilities_FacilityId",
                table: "PropertyFacilities",
                column: "FacilityId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyPhotos_PropertyId_SortOrder",
                table: "PropertyPhotos",
                columns: new[] { "PropertyId", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PropertyFacilities");

            migrationBuilder.DropTable(
                name: "PropertyPhotos");

            migrationBuilder.DropTable(
                name: "Properties");
        }
    }
}
