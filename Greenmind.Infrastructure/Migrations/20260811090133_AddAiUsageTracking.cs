using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Greenmind.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiUsageTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAiAccessSuspended",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AiUsageRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProviderRequestId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    FarmId = table.Column<int>(type: "int", nullable: true),
                    ExportPlanId = table.Column<int>(type: "int", nullable: true),
                    Feature = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ModelId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Region = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    InputTokens = table.Column<int>(type: "int", nullable: false),
                    OutputTokens = table.Column<int>(type: "int", nullable: false),
                    TotalTokens = table.Column<int>(type: "int", nullable: false),
                    StopReason = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BudgetState = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    FallbackUsed = table.Column<bool>(type: "bit", nullable: false),
                    EstimatedCostUsd = table.Column<decimal>(type: "decimal(18,8)", nullable: false),
                    ActualCostUsd = table.Column<decimal>(type: "decimal(18,8)", nullable: false),
                    ProviderStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiUsageRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiUsageRecords_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AiUsageRecords_ExportPlans_ExportPlanId",
                        column: x => x.ExportPlanId,
                        principalTable: "ExportPlans",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AiUsageRecords_Farms_FarmId",
                        column: x => x.FarmId,
                        principalTable: "Farms",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiUsageRecords_ExportPlanId",
                table: "AiUsageRecords",
                column: "ExportPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_AiUsageRecords_FarmId",
                table: "AiUsageRecords",
                column: "FarmId");

            migrationBuilder.CreateIndex(
                name: "IX_AiUsageRecords_Feature_CompletedAtUtc",
                table: "AiUsageRecords",
                columns: new[] { "Feature", "CompletedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AiUsageRecords_ModelId_CompletedAtUtc",
                table: "AiUsageRecords",
                columns: new[] { "ModelId", "CompletedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AiUsageRecords_ProviderRequestId",
                table: "AiUsageRecords",
                column: "ProviderRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiUsageRecords_UserId_CompletedAtUtc",
                table: "AiUsageRecords",
                columns: new[] { "UserId", "CompletedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiUsageRecords");

            migrationBuilder.DropColumn(
                name: "IsAiAccessSuspended",
                table: "AspNetUsers");
        }
    }
}
