using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DaineBot.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostgre : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminRoles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Guild = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    RoleList = table.Column<decimal[]>(type: "numeric(20,0)[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Rosters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Guild = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    RosterRole = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    RosterChannel = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    TimeZoneId = table.Column<string>(type: "text", nullable: false),
                    RaidLeader = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rosters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TmpSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Day = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TmpSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RaidSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RosterId = table.Column<int>(type: "integer", nullable: false),
                    ReportCode = table.Column<string>(type: "text", nullable: true),
                    Hour = table.Column<int>(type: "integer", nullable: false),
                    Minute = table.Column<int>(type: "integer", nullable: false),
                    Day = table.Column<int>(type: "integer", nullable: false),
                    Duration = table.Column<TimeSpan>(type: "interval", nullable: false),
                    NextSessionEditId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RaidSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RaidSessions_RaidSessions_NextSessionEditId",
                        column: x => x.NextSessionEditId,
                        principalTable: "RaidSessions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RaidSessions_Rosters_RosterId",
                        column: x => x.RosterId,
                        principalTable: "Rosters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReadyChecks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SessionId = table.Column<int>(type: "integer", nullable: false),
                    AcceptedPlayers = table.Column<decimal[]>(type: "numeric(20,0)[]", nullable: false),
                    DeniedPlayers = table.Column<decimal[]>(type: "numeric(20,0)[]", nullable: false),
                    ReminderSent = table.Column<bool>(type: "boolean", nullable: false),
                    Complete = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadyChecks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReadyChecks_RaidSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "RaidSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReadyCheckMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MessageId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    CheckId = table.Column<int>(type: "integer", nullable: false)
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
                name: "IX_RaidSessions_RosterId",
                table: "RaidSessions",
                column: "RosterId");

            migrationBuilder.CreateIndex(
                name: "IX_ReadyCheckMessages_CheckId",
                table: "ReadyCheckMessages",
                column: "CheckId");

            migrationBuilder.CreateIndex(
                name: "IX_ReadyChecks_SessionId",
                table: "ReadyChecks",
                column: "SessionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminRoles");

            migrationBuilder.DropTable(
                name: "ReadyCheckMessages");

            migrationBuilder.DropTable(
                name: "TmpSessions");

            migrationBuilder.DropTable(
                name: "ReadyChecks");

            migrationBuilder.DropTable(
                name: "RaidSessions");

            migrationBuilder.DropTable(
                name: "Rosters");
        }
    }
}
