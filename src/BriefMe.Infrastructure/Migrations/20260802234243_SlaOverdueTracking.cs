using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BriefMe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SlaOverdueTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "OverdueSince",
                table: "Tasks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OverdueSince",
                table: "Briefings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_OverdueSince",
                table: "Tasks",
                column: "OverdueSince");

            migrationBuilder.CreateIndex(
                name: "IX_Briefings_OverdueSince",
                table: "Briefings",
                column: "OverdueSince");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tasks_OverdueSince",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_Briefings_OverdueSince",
                table: "Briefings");

            migrationBuilder.DropColumn(
                name: "OverdueSince",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "OverdueSince",
                table: "Briefings");
        }
    }
}
