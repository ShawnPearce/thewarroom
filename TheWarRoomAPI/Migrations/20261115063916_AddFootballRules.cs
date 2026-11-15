using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TheWarRoomAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddFootballRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Playbooks_TeamId_Unit",
                table: "Playbooks");

            // The teams that already exist play American football (1), and so do their playbooks further down
            migrationBuilder.AddColumn<int>(
                name: "Rules",
                table: "Teams",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Rules",
                table: "Positions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Rules",
                table: "Playbooks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            // Three of the standard positions belong to American football only. On the teams that already exist
            // they are marked as such, so that they are put away if the team takes up Canadian football.
            migrationBuilder.Sql(
                "UPDATE Positions SET Rules = 1 " +
                "WHERE (Code = 'TE' AND Name = 'Tight End') " +
                "OR (Code = 'FS' AND Name = 'Free Safety') " +
                "OR (Code = 'SS' AND Name = 'Strong Safety');");

            migrationBuilder.CreateIndex(
                name: "IX_Playbooks_TeamId_Unit_Rules",
                table: "Playbooks",
                columns: new[] { "TeamId", "Unit", "Rules" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Playbooks_TeamId_Unit_Rules",
                table: "Playbooks");

            migrationBuilder.DropColumn(
                name: "Rules",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "Rules",
                table: "Positions");

            migrationBuilder.DropColumn(
                name: "Rules",
                table: "Playbooks");

            migrationBuilder.CreateIndex(
                name: "IX_Playbooks_TeamId_Unit",
                table: "Playbooks",
                columns: new[] { "TeamId", "Unit" },
                unique: true);
        }
    }
}
