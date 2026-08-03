using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Greenmind.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TrustedScheduleSourceMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "EffectiveDate",
                table: "ComplianceDocuments",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Publisher",
                table: "ComplianceDocuments",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetrievedAtUtc",
                table: "ComplianceDocuments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceUrl",
                table: "ComplianceDocuments",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrustLevel",
                table: "ComplianceDocuments",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Official");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EffectiveDate",
                table: "ComplianceDocuments");

            migrationBuilder.DropColumn(
                name: "Publisher",
                table: "ComplianceDocuments");

            migrationBuilder.DropColumn(
                name: "RetrievedAtUtc",
                table: "ComplianceDocuments");

            migrationBuilder.DropColumn(
                name: "SourceUrl",
                table: "ComplianceDocuments");

            migrationBuilder.DropColumn(
                name: "TrustLevel",
                table: "ComplianceDocuments");
        }
    }
}
