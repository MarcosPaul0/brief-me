using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BriefMe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BriefingDesiredDeliveryDateAsDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateOnly>(
                name: "DesiredDeliveryDate",
                table: "Briefings",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "DesiredDeliveryDate",
                table: "Briefings",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);
        }
    }
}
