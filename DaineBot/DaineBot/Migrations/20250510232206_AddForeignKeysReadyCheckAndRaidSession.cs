using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DaineBot.Migrations
{
    /// <inheritdoc />
    public partial class AddForeignKeysReadyCheckAndRaidSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RaidSessions_Rosters_Id",
                table: "RaidSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_ReadyChecks_RaidSessions_Id",
                table: "ReadyChecks");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "ReadyChecks",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .Annotation("Sqlite:Autoincrement", true);

            migrationBuilder.AddColumn<int>(
                name: "SessionId",
                table: "ReadyChecks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "RaidSessions",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .Annotation("Sqlite:Autoincrement", true);

            migrationBuilder.AddColumn<int>(
                name: "RosterId",
                table: "RaidSessions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ReadyChecks_SessionId",
                table: "ReadyChecks",
                column: "SessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RaidSessions_RosterId",
                table: "RaidSessions",
                column: "RosterId");

            migrationBuilder.AddForeignKey(
                name: "FK_RaidSessions_Rosters_RosterId",
                table: "RaidSessions",
                column: "RosterId",
                principalTable: "Rosters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ReadyChecks_RaidSessions_SessionId",
                table: "ReadyChecks",
                column: "SessionId",
                principalTable: "RaidSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RaidSessions_Rosters_RosterId",
                table: "RaidSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_ReadyChecks_RaidSessions_SessionId",
                table: "ReadyChecks");

            migrationBuilder.DropIndex(
                name: "IX_ReadyChecks_SessionId",
                table: "ReadyChecks");

            migrationBuilder.DropIndex(
                name: "IX_RaidSessions_RosterId",
                table: "RaidSessions");

            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "ReadyChecks");

            migrationBuilder.DropColumn(
                name: "RosterId",
                table: "RaidSessions");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "ReadyChecks",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .OldAnnotation("Sqlite:Autoincrement", true);

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "RaidSessions",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .OldAnnotation("Sqlite:Autoincrement", true);

            migrationBuilder.AddForeignKey(
                name: "FK_RaidSessions_Rosters_Id",
                table: "RaidSessions",
                column: "Id",
                principalTable: "Rosters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ReadyChecks_RaidSessions_Id",
                table: "ReadyChecks",
                column: "Id",
                principalTable: "RaidSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
