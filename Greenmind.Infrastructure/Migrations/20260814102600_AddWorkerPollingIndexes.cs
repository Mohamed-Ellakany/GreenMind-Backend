using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Greenmind.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkerPollingIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PlanningStatus",
                table: "ExportPlans",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "ExportPlans",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleRevisions_Status_CreatedAt",
                table: "ScheduleRevisions",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ExportPlans_PlanningStatus_NextPlanningAttemptAtUtc_PlanningRequestedAtUtc",
                table: "ExportPlans",
                columns: new[] { "PlanningStatus", "NextPlanningAttemptAtUtc", "PlanningRequestedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ScheduleRevisions_Status_CreatedAt",
                table: "ScheduleRevisions");

            migrationBuilder.DropIndex(
                name: "IX_ExportPlans_PlanningStatus_NextPlanningAttemptAtUtc_PlanningRequestedAtUtc",
                table: "ExportPlans");

            migrationBuilder.AlterColumn<string>(
                name: "PlanningStatus",
                table: "ExportPlans",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "ExportPlans",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32);
        }
    }
}
