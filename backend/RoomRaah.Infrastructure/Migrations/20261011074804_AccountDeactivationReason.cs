using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoomRaah.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AccountDeactivationReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeactivationReason",
                table: "AppUsers",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeactivationReason",
                table: "AppUsers");
        }
    }
}
