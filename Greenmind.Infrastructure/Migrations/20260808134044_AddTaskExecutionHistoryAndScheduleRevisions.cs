using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Greenmind.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskExecutionHistoryAndScheduleRevisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FarmTasks_ExportPlanId_TaskTypeId_PlannedDate",
                table: "FarmTasks");

            migrationBuilder.AddColumn<string>(
                name: "Origin",
                table: "FarmTasks",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "AIPlanned");

            migrationBuilder.CreateTable(
                name: "FarmTaskActivities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FarmTaskId = table.Column<int>(type: "int", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ActorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PreviousStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    NewStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    PreviousPlannedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    NewPlannedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ActualDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FarmTaskActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FarmTaskActivities_FarmTasks_FarmTaskId",
                        column: x => x.FarmTaskId,
                        principalTable: "FarmTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleRevisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExportPlanId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    FailureMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduleRevisions_ExportPlans_ExportPlanId",
                        column: x => x.ExportPlanId,
                        principalTable: "ExportPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleRevisionItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ScheduleRevisionId = table.Column<int>(type: "int", nullable: false),
                    FarmTaskId = table.Column<int>(type: "int", nullable: true),
                    Operation = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PreviousPlannedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    NewPlannedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    TaskTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleRevisionItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduleRevisionItems_FarmTasks_FarmTaskId",
                        column: x => x.FarmTaskId,
                        principalTable: "FarmTasks",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ScheduleRevisionItems_ScheduleRevisions_ScheduleRevisionId",
                        column: x => x.ScheduleRevisionId,
                        principalTable: "ScheduleRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "TaskTypes",
                columns: new[] { "Id", "CreatedAt", "Description", "Name" },
                values: new object[] { 9, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Other farm activity recorded by the user", "Other" });

            migrationBuilder.CreateIndex(
                name: "IX_FarmTasks_ExportPlanId_PlannedDate",
                table: "FarmTasks",
                columns: new[] { "ExportPlanId", "PlannedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_FarmTaskActivities_FarmTaskId_CreatedAt",
                table: "FarmTaskActivities",
                columns: new[] { "FarmTaskId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleRevisionItems_FarmTaskId",
                table: "ScheduleRevisionItems",
                column: "FarmTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleRevisionItems_ScheduleRevisionId",
                table: "ScheduleRevisionItems",
                column: "ScheduleRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleRevisions_ExportPlanId_Status_CreatedAt",
                table: "ScheduleRevisions",
                columns: new[] { "ExportPlanId", "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FarmTaskActivities");

            migrationBuilder.DropTable(
                name: "ScheduleRevisionItems");

            migrationBuilder.DropTable(
                name: "ScheduleRevisions");

            migrationBuilder.DropIndex(
                name: "IX_FarmTasks_ExportPlanId_PlannedDate",
                table: "FarmTasks");

            migrationBuilder.DeleteData(
                table: "TaskTypes",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DropColumn(
                name: "Origin",
                table: "FarmTasks");

            migrationBuilder.CreateIndex(
                name: "IX_FarmTasks_ExportPlanId_TaskTypeId_PlannedDate",
                table: "FarmTasks",
                columns: new[] { "ExportPlanId", "TaskTypeId", "PlannedDate" },
                unique: true);
        }
    }
}
