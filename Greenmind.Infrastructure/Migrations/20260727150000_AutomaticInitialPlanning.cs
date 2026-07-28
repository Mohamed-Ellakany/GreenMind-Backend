using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Greenmind.Infrastructure.Migrations;

[DbContext(typeof(Persistence.GreenmindDbContext))]
[Migration("20260727150000_AutomaticInitialPlanning")]
public partial class AutomaticInitialPlanning : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "GeneratedTaskCount",
            table: "ExportPlans",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<DateTime>(
            name: "NextPlanningAttemptAtUtc",
            table: "ExportPlans",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "PlanningAttemptCount",
            table: "ExportPlans",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<DateTime>(
            name: "PlanningCompletedAtUtc",
            table: "ExportPlans",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PlanningFailureMessage",
            table: "ExportPlans",
            type: "nvarchar(500)",
            maxLength: 500,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "PlanningRequestedAtUtc",
            table: "ExportPlans",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "PlanningStartedAtUtc",
            table: "ExportPlans",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PlanningStatus",
            table: "ExportPlans",
            type: "nvarchar(max)",
            nullable: false,
            defaultValue: "Succeeded");

        migrationBuilder.Sql("""
            WITH duplicates AS (
                SELECT Id, ROW_NUMBER() OVER (
                    PARTITION BY ExportPlanId, TaskTypeId, PlannedDate
                    ORDER BY Id) AS RowNumber
                FROM FarmTasks
            )
            DELETE FROM FarmTasks WHERE Id IN (
                SELECT Id FROM duplicates WHERE RowNumber > 1
            );
            """);

        migrationBuilder.CreateIndex(
            name: "IX_FarmTasks_ExportPlanId_TaskTypeId_PlannedDate",
            table: "FarmTasks",
            columns: new[] { "ExportPlanId", "TaskTypeId", "PlannedDate" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_FarmTasks_ExportPlanId_TaskTypeId_PlannedDate",
            table: "FarmTasks");

        migrationBuilder.DropColumn(name: "GeneratedTaskCount", table: "ExportPlans");
        migrationBuilder.DropColumn(name: "NextPlanningAttemptAtUtc", table: "ExportPlans");
        migrationBuilder.DropColumn(name: "PlanningAttemptCount", table: "ExportPlans");
        migrationBuilder.DropColumn(name: "PlanningCompletedAtUtc", table: "ExportPlans");
        migrationBuilder.DropColumn(name: "PlanningFailureMessage", table: "ExportPlans");
        migrationBuilder.DropColumn(name: "PlanningRequestedAtUtc", table: "ExportPlans");
        migrationBuilder.DropColumn(name: "PlanningStartedAtUtc", table: "ExportPlans");
        migrationBuilder.DropColumn(name: "PlanningStatus", table: "ExportPlans");
    }
}
