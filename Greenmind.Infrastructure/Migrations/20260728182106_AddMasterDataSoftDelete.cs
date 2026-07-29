using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Greenmind.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMasterDataSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Destinations_Name",
                table: "Destinations");

            migrationBuilder.DropIndex(
                name: "IX_Crops_Name",
                table: "Crops");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                table: "Destinations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Destinations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                table: "Crops",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Crops",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "Crops",
                keyColumn: "Id",
                keyValue: 1,
                column: "DeletedAtUtc",
                value: null);

            migrationBuilder.UpdateData(
                table: "Crops",
                keyColumn: "Id",
                keyValue: 2,
                column: "DeletedAtUtc",
                value: null);

            migrationBuilder.UpdateData(
                table: "Crops",
                keyColumn: "Id",
                keyValue: 3,
                column: "DeletedAtUtc",
                value: null);

            migrationBuilder.UpdateData(
                table: "Crops",
                keyColumn: "Id",
                keyValue: 4,
                column: "DeletedAtUtc",
                value: null);

            migrationBuilder.UpdateData(
                table: "Crops",
                keyColumn: "Id",
                keyValue: 5,
                column: "DeletedAtUtc",
                value: null);

            migrationBuilder.UpdateData(
                table: "Crops",
                keyColumn: "Id",
                keyValue: 6,
                column: "DeletedAtUtc",
                value: null);

            migrationBuilder.UpdateData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 1,
                column: "DeletedAtUtc",
                value: null);

            migrationBuilder.UpdateData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 2,
                column: "DeletedAtUtc",
                value: null);

            migrationBuilder.UpdateData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 3,
                column: "DeletedAtUtc",
                value: null);

            migrationBuilder.UpdateData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 4,
                column: "DeletedAtUtc",
                value: null);

            migrationBuilder.UpdateData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 5,
                column: "DeletedAtUtc",
                value: null);

            migrationBuilder.UpdateData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 6,
                column: "DeletedAtUtc",
                value: null);

            migrationBuilder.UpdateData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 7,
                column: "DeletedAtUtc",
                value: null);

            migrationBuilder.UpdateData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 8,
                column: "DeletedAtUtc",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_Destinations_Name",
                table: "Destinations",
                column: "Name",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Crops_Name",
                table: "Crops",
                column: "Name",
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Destinations_Name",
                table: "Destinations");

            migrationBuilder.DropIndex(
                name: "IX_Crops_Name",
                table: "Crops");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "Destinations");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Destinations");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "Crops");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Crops");

            migrationBuilder.CreateIndex(
                name: "IX_Destinations_Name",
                table: "Destinations",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Crops_Name",
                table: "Crops",
                column: "Name",
                unique: true);
        }
    }
}
