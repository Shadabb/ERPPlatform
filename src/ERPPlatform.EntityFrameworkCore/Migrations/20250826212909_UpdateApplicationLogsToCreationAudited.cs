using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERPPlatform.Migrations
{
    /// <inheritdoc />
    public partial class UpdateApplicationLogsToCreationAudited : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConcurrencyStamp",
                table: "ApplicationLogs",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreationTime",
                table: "ApplicationLogs",
                type: "timestamp without time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "CreatorId",
                table: "ApplicationLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExtraProperties",
                table: "ApplicationLogs",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                table: "ApplicationLogs");

            migrationBuilder.DropColumn(
                name: "CreationTime",
                table: "ApplicationLogs");

            migrationBuilder.DropColumn(
                name: "CreatorId",
                table: "ApplicationLogs");

            migrationBuilder.DropColumn(
                name: "ExtraProperties",
                table: "ApplicationLogs");
        }
    }
}
