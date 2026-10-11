using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoomRaah.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConversationDeletes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HiddenFromOwner",
                table: "Conversations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HiddenFromSeeker",
                table: "Conversations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "OwnerDeletedAt",
                table: "Conversations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SeekerDeletedAt",
                table: "Conversations",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HiddenFromOwner",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "HiddenFromSeeker",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "OwnerDeletedAt",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "SeekerDeletedAt",
                table: "Conversations");
        }
    }
}
