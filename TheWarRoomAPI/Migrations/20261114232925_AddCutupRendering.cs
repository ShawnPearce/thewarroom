using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TheWarRoomAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddCutupRendering : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DurationMs",
                table: "Cutups",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProgressPercent",
                table: "Cutups",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "QueuedAt",
                table: "Cutups",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RenderedAt",
                table: "Cutups",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OutputDurationMs",
                table: "CutupClips",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OutputOffsetMs",
                table: "CutupClips",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DurationMs",
                table: "Cutups");

            migrationBuilder.DropColumn(
                name: "ProgressPercent",
                table: "Cutups");

            migrationBuilder.DropColumn(
                name: "QueuedAt",
                table: "Cutups");

            migrationBuilder.DropColumn(
                name: "RenderedAt",
                table: "Cutups");

            migrationBuilder.DropColumn(
                name: "OutputDurationMs",
                table: "CutupClips");

            migrationBuilder.DropColumn(
                name: "OutputOffsetMs",
                table: "CutupClips");
        }
    }
}
