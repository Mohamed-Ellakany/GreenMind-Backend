using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Greenmind.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRegulatoryData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActiveSubstances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RegionName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ExternalId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CasNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ApprovalStatus = table.Column<byte>(type: "tinyint", nullable: false),
                    ApprovalExpiresOn = table.Column<DateOnly>(type: "date", nullable: true),
                    Legislation = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RegulationReference = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Functions = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActiveSubstances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductMrls",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RegionName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProductType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ExternalPesticideId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ResidueName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MaximumResidueLevel = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    IsDetectionLimit = table.Column<bool>(type: "bit", nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductMrls", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActiveSubstances_RegionName_CasNumber",
                table: "ActiveSubstances",
                columns: new[] { "RegionName", "CasNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_ActiveSubstances_RegionName_ExternalId",
                table: "ActiveSubstances",
                columns: new[] { "RegionName", "ExternalId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ActiveSubstances_RegionName_Name",
                table: "ActiveSubstances",
                columns: new[] { "RegionName", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductMrls_RegionName_ProductType",
                table: "ProductMrls",
                columns: new[] { "RegionName", "ProductType" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductMrls_RegionName_ProductType_ExternalPesticideId",
                table: "ProductMrls",
                columns: new[] { "RegionName", "ProductType", "ExternalPesticideId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActiveSubstances");

            migrationBuilder.DropTable(
                name: "ProductMrls");
        }
    }
}
