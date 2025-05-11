using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DaineBot.Migrations
{
    /// <inheritdoc />
    public partial class UpdateRaidSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EndTime",
                table: "RaidSessions");

            migrationBuilder.RenameColumn(
                name: "StartTime",
                table: "RaidSessions",
                newName: "Duration");

            migrationBuilder.AddColumn<int>(
                name: "Day",
                table: "RaidSessions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Hour",
                table: "RaidSessions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Minute",
                table: "RaidSessions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Day",
                table: "RaidSessions");

            migrationBuilder.DropColumn(
                name: "Hour",
                table: "RaidSessions");

            migrationBuilder.DropColumn(
                name: "Minute",
                table: "RaidSessions");

            migrationBuilder.RenameColumn(
                name: "Duration",
                table: "RaidSessions",
                newName: "StartTime");

            migrationBuilder.AddColumn<DateTime>(
                name: "EndTime",
                table: "RaidSessions",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }
    }
}
