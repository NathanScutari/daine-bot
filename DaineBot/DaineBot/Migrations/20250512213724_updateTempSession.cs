using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DaineBot.Migrations
{
    /// <inheritdoc />
    public partial class updateTempSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RaidSessions_RaidSessions_NextSessionEditId",
                table: "RaidSessions");

            migrationBuilder.DropIndex(
                name: "IX_RaidSessions_NextSessionEditId",
                table: "RaidSessions");

            migrationBuilder.DropColumn(
                name: "NextSessionEditId",
                table: "RaidSessions");

            migrationBuilder.AddColumn<DateTime>(
                name: "ReplacedSession",
                table: "RaidSessions",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReplacedSession",
                table: "RaidSessions");

            migrationBuilder.AddColumn<int>(
                name: "NextSessionEditId",
                table: "RaidSessions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RaidSessions_NextSessionEditId",
                table: "RaidSessions",
                column: "NextSessionEditId");

            migrationBuilder.AddForeignKey(
                name: "FK_RaidSessions_RaidSessions_NextSessionEditId",
                table: "RaidSessions",
                column: "NextSessionEditId",
                principalTable: "RaidSessions",
                principalColumn: "Id");
        }
    }
}
