using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Greenmind.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExportPlanFarmDataAndAnalyses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FarmType",
                table: "ExportPlans",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Fertigation",
                table: "ExportPlans",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IrrigationSystem",
                table: "ExportPlans",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "SoilEc",
                table: "ExportPlans",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "SoilNitrogen",
                table: "ExportPlans",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "SoilOrganicMatter",
                table: "ExportPlans",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "SoilPh",
                table: "ExportPlans",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "SoilPhosphorus",
                table: "ExportPlans",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "SoilPotassium",
                table: "ExportPlans",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SoilType",
                table: "ExportPlans",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "WaterBicarbonate",
                table: "ExportPlans",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "WaterCalcium",
                table: "ExportPlans",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "WaterChloride",
                table: "ExportPlans",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "WaterEc",
                table: "ExportPlans",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "WaterMagnesium",
                table: "ExportPlans",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "WaterPh",
                table: "ExportPlans",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "WaterSalinity",
                table: "ExportPlans",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "WaterSar",
                table: "ExportPlans",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "WaterSodium",
                table: "ExportPlans",
                type: "float",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FarmType",
                table: "ExportPlans");

            migrationBuilder.DropColumn(
                name: "Fertigation",
                table: "ExportPlans");

            migrationBuilder.DropColumn(
                name: "IrrigationSystem",
                table: "ExportPlans");

            migrationBuilder.DropColumn(
                name: "SoilEc",
                table: "ExportPlans");

            migrationBuilder.DropColumn(
                name: "SoilNitrogen",
                table: "ExportPlans");

            migrationBuilder.DropColumn(
                name: "SoilOrganicMatter",
                table: "ExportPlans");

            migrationBuilder.DropColumn(
                name: "SoilPh",
                table: "ExportPlans");

            migrationBuilder.DropColumn(
                name: "SoilPhosphorus",
                table: "ExportPlans");

            migrationBuilder.DropColumn(
                name: "SoilPotassium",
                table: "ExportPlans");

            migrationBuilder.DropColumn(
                name: "SoilType",
                table: "ExportPlans");

            migrationBuilder.DropColumn(
                name: "WaterBicarbonate",
                table: "ExportPlans");

            migrationBuilder.DropColumn(
                name: "WaterCalcium",
                table: "ExportPlans");

            migrationBuilder.DropColumn(
                name: "WaterChloride",
                table: "ExportPlans");

            migrationBuilder.DropColumn(
                name: "WaterEc",
                table: "ExportPlans");

            migrationBuilder.DropColumn(
                name: "WaterMagnesium",
                table: "ExportPlans");

            migrationBuilder.DropColumn(
                name: "WaterPh",
                table: "ExportPlans");

            migrationBuilder.DropColumn(
                name: "WaterSalinity",
                table: "ExportPlans");

            migrationBuilder.DropColumn(
                name: "WaterSar",
                table: "ExportPlans");

            migrationBuilder.DropColumn(
                name: "WaterSodium",
                table: "ExportPlans");
        }
    }
}
