using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoomRaah.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InspectionsAndBadges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "InspectionRequestedAt",
                table: "Properties",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BadgeEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PropertyId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ActedByAdminId = table.Column<int>(type: "int", nullable: false),
                    ActedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BadgeEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BadgeEvents_AppUsers_ActedByAdminId",
                        column: x => x.ActedByAdminId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BadgeEvents_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PropertyInspections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PropertyId = table.Column<int>(type: "int", nullable: false),
                    InspectedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    InspectorAdminId = table.Column<int>(type: "int", nullable: false),
                    Result = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    FeeAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    FeeCollectedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyInspections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropertyInspections_AppUsers_InspectorAdminId",
                        column: x => x.InspectorAdminId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PropertyInspections_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppUsers_Role_IsActive",
                table: "AppUsers",
                columns: new[] { "Role", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_BadgeEvents_ActedByAdminId",
                table: "BadgeEvents",
                column: "ActedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_BadgeEvents_PropertyId_ActedAt",
                table: "BadgeEvents",
                columns: new[] { "PropertyId", "ActedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PropertyInspections_InspectorAdminId",
                table: "PropertyInspections",
                column: "InspectorAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyInspections_PropertyId_InspectedAt",
                table: "PropertyInspections",
                columns: new[] { "PropertyId", "InspectedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BadgeEvents");

            migrationBuilder.DropTable(
                name: "PropertyInspections");

            migrationBuilder.DropIndex(
                name: "IX_AppUsers_Role_IsActive",
                table: "AppUsers");

            migrationBuilder.DropColumn(
                name: "InspectionRequestedAt",
                table: "Properties");
        }
    }
}
