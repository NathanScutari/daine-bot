using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DaineBot.Migrations
{
    /// <inheritdoc />
    public partial class AddReadyCheckComplete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Complete",
                table: "ReadyChecks",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Complete",
                table: "ReadyChecks");
        }
    }
}
