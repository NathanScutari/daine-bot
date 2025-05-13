using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DaineBot.Migrations
{
    /// <inheritdoc />
    public partial class addNextSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ReplacedSession",
                table: "RaidSessions",
                newName: "NextSession");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "NextSession",
                table: "RaidSessions",
                newName: "ReplacedSession");
        }
    }
}
