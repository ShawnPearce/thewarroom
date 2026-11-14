using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TheWarRoomAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddStudyTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ViewingSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TeamId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    CutupId = table.Column<int>(type: "INTEGER", nullable: true),
                    PlaybookId = table.Column<int>(type: "INTEGER", nullable: true),
                    PageKey = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    LoginSessionId = table.Column<int>(type: "INTEGER", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastCreditAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ActiveMs = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ViewingSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ViewingSessions_Cutups_CutupId",
                        column: x => x.CutupId,
                        principalTable: "Cutups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ViewingSessions_LoginSessions_LoginSessionId",
                        column: x => x.LoginSessionId,
                        principalTable: "LoginSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ViewingSessions_Playbooks_PlaybookId",
                        column: x => x.PlaybookId,
                        principalTable: "Playbooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ViewingSessions_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ViewingSessions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ViewingSessions_CutupId",
                table: "ViewingSessions",
                column: "CutupId");

            migrationBuilder.CreateIndex(
                name: "IX_ViewingSessions_LoginSessionId",
                table: "ViewingSessions",
                column: "LoginSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_ViewingSessions_PlaybookId",
                table: "ViewingSessions",
                column: "PlaybookId");

            migrationBuilder.CreateIndex(
                name: "IX_ViewingSessions_TeamId",
                table: "ViewingSessions",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_ViewingSessions_UserId_PageKey",
                table: "ViewingSessions",
                columns: new[] { "UserId", "PageKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ViewingSessions");
        }
    }
}
