using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BriefMe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BriefingApprovalDecision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DecisionJustification",
                table: "Briefings",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "Briefings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewedById",
                table: "Briefings",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DecisionJustification",
                table: "Briefings");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "Briefings");

            migrationBuilder.DropColumn(
                name: "ReviewedById",
                table: "Briefings");
        }
    }
}
