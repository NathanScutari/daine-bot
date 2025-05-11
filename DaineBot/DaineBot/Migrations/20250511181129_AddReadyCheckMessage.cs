using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DaineBot.Migrations
{
    /// <inheritdoc />
    public partial class AddReadyCheckMessage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NextSessionEditId",
                table: "RaidSessions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ReadyCheckMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MessageId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    UserId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    CheckId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadyCheckMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReadyCheckMessages_ReadyChecks_CheckId",
                        column: x => x.CheckId,
                        principalTable: "ReadyChecks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RaidSessions_NextSessionEditId",
                table: "RaidSessions",
                column: "NextSessionEditId");

            migrationBuilder.CreateIndex(
                name: "IX_ReadyCheckMessages_CheckId",
                table: "ReadyCheckMessages",
                column: "CheckId");

            migrationBuilder.AddForeignKey(
                name: "FK_RaidSessions_RaidSessions_NextSessionEditId",
                table: "RaidSessions",
                column: "NextSessionEditId",
                principalTable: "RaidSessions",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RaidSessions_RaidSessions_NextSessionEditId",
                table: "RaidSessions");

            migrationBuilder.DropTable(
                name: "ReadyCheckMessages");

            migrationBuilder.DropIndex(
                name: "IX_RaidSessions_NextSessionEditId",
                table: "RaidSessions");

            migrationBuilder.DropColumn(
                name: "NextSessionEditId",
                table: "RaidSessions");
        }
    }
}
