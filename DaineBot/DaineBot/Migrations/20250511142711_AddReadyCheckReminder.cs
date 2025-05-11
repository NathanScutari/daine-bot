using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DaineBot.Migrations
{
    /// <inheritdoc />
    public partial class AddReadyCheckReminder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ReminderSent",
                table: "ReadyChecks",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReminderSent",
                table: "ReadyChecks");
        }
    }
}
