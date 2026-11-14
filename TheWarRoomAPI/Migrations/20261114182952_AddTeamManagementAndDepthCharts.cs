using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TheWarRoomAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamManagementAndDepthCharts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DepthChartWeeks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TeamId = table.Column<int>(type: "INTEGER", nullable: false),
                    WeekStartDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Label = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepthChartWeeks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DepthChartWeeks_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PositionGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TeamId = table.Column<int>(type: "INTEGER", nullable: false),
                    Unit = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, collation: "NOCASE"),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PositionGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PositionGroups_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RosterEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TeamId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    JerseyNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    Grade = table.Column<int>(type: "INTEGER", nullable: true),
                    HeightInches = table.Column<int>(type: "INTEGER", nullable: true),
                    WeightLbs = table.Column<int>(type: "INTEGER", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RosterEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RosterEntries_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RosterEntries_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CoachGroupAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PositionGroupId = table.Column<int>(type: "INTEGER", nullable: false),
                    CoachId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoachGroupAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CoachGroupAssignments_PositionGroups_PositionGroupId",
                        column: x => x.PositionGroupId,
                        principalTable: "PositionGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CoachGroupAssignments_Users_CoachId",
                        column: x => x.CoachId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlayerGroupAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PositionGroupId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlayerId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerGroupAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerGroupAssignments_PositionGroups_PositionGroupId",
                        column: x => x.PositionGroupId,
                        principalTable: "PositionGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlayerGroupAssignments_Users_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Positions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TeamId = table.Column<int>(type: "INTEGER", nullable: false),
                    PositionGroupId = table.Column<int>(type: "INTEGER", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 6, nullable: false, collation: "NOCASE"),
                    Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Positions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Positions_PositionGroups_PositionGroupId",
                        column: x => x.PositionGroupId,
                        principalTable: "PositionGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Positions_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DepthChartPlacements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TeamId = table.Column<int>(type: "INTEGER", nullable: false),
                    DepthChartWeekId = table.Column<int>(type: "INTEGER", nullable: false),
                    PositionId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlayerId = table.Column<int>(type: "INTEGER", nullable: false),
                    Rank = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepthChartPlacements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DepthChartPlacements_DepthChartWeeks_DepthChartWeekId",
                        column: x => x.DepthChartWeekId,
                        principalTable: "DepthChartWeeks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DepthChartPlacements_Positions_PositionId",
                        column: x => x.PositionId,
                        principalTable: "Positions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DepthChartPlacements_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DepthChartPlacements_Users_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CoachGroupAssignments_CoachId_PositionGroupId",
                table: "CoachGroupAssignments",
                columns: new[] { "CoachId", "PositionGroupId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CoachGroupAssignments_PositionGroupId",
                table: "CoachGroupAssignments",
                column: "PositionGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_DepthChartPlacements_DepthChartWeekId_PositionId_PlayerId",
                table: "DepthChartPlacements",
                columns: new[] { "DepthChartWeekId", "PositionId", "PlayerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DepthChartPlacements_PlayerId",
                table: "DepthChartPlacements",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_DepthChartPlacements_PositionId",
                table: "DepthChartPlacements",
                column: "PositionId");

            migrationBuilder.CreateIndex(
                name: "IX_DepthChartPlacements_TeamId",
                table: "DepthChartPlacements",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_DepthChartWeeks_TeamId_WeekStartDate",
                table: "DepthChartWeeks",
                columns: new[] { "TeamId", "WeekStartDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerGroupAssignments_PlayerId_PositionGroupId",
                table: "PlayerGroupAssignments",
                columns: new[] { "PlayerId", "PositionGroupId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerGroupAssignments_PositionGroupId",
                table: "PlayerGroupAssignments",
                column: "PositionGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_PositionGroups_TeamId_Unit_Name",
                table: "PositionGroups",
                columns: new[] { "TeamId", "Unit", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Positions_PositionGroupId",
                table: "Positions",
                column: "PositionGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Positions_TeamId_Code",
                table: "Positions",
                columns: new[] { "TeamId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RosterEntries_TeamId",
                table: "RosterEntries",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_RosterEntries_UserId",
                table: "RosterEntries",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CoachGroupAssignments");

            migrationBuilder.DropTable(
                name: "DepthChartPlacements");

            migrationBuilder.DropTable(
                name: "PlayerGroupAssignments");

            migrationBuilder.DropTable(
                name: "RosterEntries");

            migrationBuilder.DropTable(
                name: "DepthChartWeeks");

            migrationBuilder.DropTable(
                name: "Positions");

            migrationBuilder.DropTable(
                name: "PositionGroups");
        }
    }
}
