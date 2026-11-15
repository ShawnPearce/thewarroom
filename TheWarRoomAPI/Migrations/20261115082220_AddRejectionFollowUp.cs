using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TheWarRoomAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddRejectionFollowUp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DecisionSeenAt",
                table: "ApprovalRequests",
                type: "TEXT",
                nullable: true);

            // The column is added together with its foreign key, in one statement. SQLite can do that to a table
            // as it stands; adding a foreign key to a column that is already there would make it rebuild the table,
            // which cannot be done inside the migration's transaction.
            migrationBuilder.Sql(
                "ALTER TABLE \"ApprovalRequests\" ADD COLUMN \"ResubmittedFromId\" INTEGER NULL " +
                "CONSTRAINT \"FK_ApprovalRequests_ApprovalRequests_ResubmittedFromId\" " +
                "REFERENCES \"ApprovalRequests\" (\"Id\") ON DELETE RESTRICT;");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequests_ResubmittedFromId",
                table: "ApprovalRequests",
                column: "ResubmittedFromId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApprovalRequests_ApprovalRequests_ResubmittedFromId",
                table: "ApprovalRequests");

            migrationBuilder.DropIndex(
                name: "IX_ApprovalRequests_ResubmittedFromId",
                table: "ApprovalRequests");

            migrationBuilder.DropColumn(
                name: "DecisionSeenAt",
                table: "ApprovalRequests");

            migrationBuilder.DropColumn(
                name: "ResubmittedFromId",
                table: "ApprovalRequests");
        }
    }
}
