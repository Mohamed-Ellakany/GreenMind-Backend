using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Greenmind.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDiagnosisConversations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ChatSessions_Scope",
                table: "ChatSessions");

            migrationBuilder.DropIndex(
                name: "IX_TaskChangeProposals_SourceDiseaseReportId",
                table: "TaskChangeProposals");

            migrationBuilder.AddColumn<bool>(
                name: "IsIncluded",
                table: "TaskChangeProposalItems",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "CaseFactsJson",
                table: "DiseaseReports",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContextSnapshotJson",
                table: "DiseaseReports",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ChatSessionId",
                table: "DiseaseReports",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedAtUtc",
                table: "DiseaseReports",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "FinalizedWithOverride",
                table: "DiseaseReports",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "KnowledgeFailureMessage",
                table: "DiseaseReports",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "KnowledgeVerified",
                table: "DiseaseReports",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ReadinessJson",
                table: "DiseaseReports",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ChatMessageId",
                table: "DiseaseImages",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DiseaseReportSources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DiseaseReportId = table.Column<int>(type: "int", nullable: false),
                    SourceName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SourceUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Publisher = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SectionReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Excerpt = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RetrievedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsOfficialOrAcademic = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiseaseReportSources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiseaseReportSources_DiseaseReports_DiseaseReportId",
                        column: x => x.DiseaseReportId,
                        principalTable: "DiseaseReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiseaseReports_ChatSessionId",
                table: "DiseaseReports",
                column: "ChatSessionId",
                unique: true,
                filter: "[ChatSessionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TaskChangeProposals_SourceDiseaseReportId",
                table: "TaskChangeProposals",
                column: "SourceDiseaseReportId",
                unique: true,
                filter: "[SourceDiseaseReportId] IS NOT NULL AND [Status] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_DiseaseImages_ChatMessageId",
                table: "DiseaseImages",
                column: "ChatMessageId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ChatSessions_Scope",
                table: "ChatSessions",
                sql: "([Scope] IN (1, 3) AND [ExportPlanId] IS NOT NULL) OR ([Scope] = 2 AND [ExportPlanId] IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_DiseaseReportSources_DiseaseReportId_SourceUrl",
                table: "DiseaseReportSources",
                columns: new[] { "DiseaseReportId", "SourceUrl" });

            migrationBuilder.AddForeignKey(
                name: "FK_DiseaseImages_ChatMessages_ChatMessageId",
                table: "DiseaseImages",
                column: "ChatMessageId",
                principalTable: "ChatMessages",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_DiseaseReports_ChatSessions_ChatSessionId",
                table: "DiseaseReports",
                column: "ChatSessionId",
                principalTable: "ChatSessions",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DiseaseImages_ChatMessages_ChatMessageId",
                table: "DiseaseImages");

            migrationBuilder.DropForeignKey(
                name: "FK_DiseaseReports_ChatSessions_ChatSessionId",
                table: "DiseaseReports");

            migrationBuilder.DropTable(
                name: "DiseaseReportSources");

            migrationBuilder.DropIndex(
                name: "IX_DiseaseReports_ChatSessionId",
                table: "DiseaseReports");

            migrationBuilder.DropIndex(
                name: "IX_DiseaseImages_ChatMessageId",
                table: "DiseaseImages");

            migrationBuilder.DropIndex(
                name: "IX_TaskChangeProposals_SourceDiseaseReportId",
                table: "TaskChangeProposals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ChatSessions_Scope",
                table: "ChatSessions");

            migrationBuilder.DropColumn(
                name: "IsIncluded",
                table: "TaskChangeProposalItems");

            migrationBuilder.DropColumn(
                name: "CaseFactsJson",
                table: "DiseaseReports");

            migrationBuilder.DropColumn(
                name: "ContextSnapshotJson",
                table: "DiseaseReports");

            migrationBuilder.DropColumn(
                name: "ChatSessionId",
                table: "DiseaseReports");

            migrationBuilder.DropColumn(
                name: "ClosedAtUtc",
                table: "DiseaseReports");

            migrationBuilder.DropColumn(
                name: "FinalizedWithOverride",
                table: "DiseaseReports");

            migrationBuilder.DropColumn(
                name: "KnowledgeFailureMessage",
                table: "DiseaseReports");

            migrationBuilder.DropColumn(
                name: "KnowledgeVerified",
                table: "DiseaseReports");

            migrationBuilder.DropColumn(
                name: "ReadinessJson",
                table: "DiseaseReports");

            migrationBuilder.DropColumn(
                name: "ChatMessageId",
                table: "DiseaseImages");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ChatSessions_Scope",
                table: "ChatSessions",
                sql: "([Scope] = 1 AND [ExportPlanId] IS NOT NULL) OR ([Scope] = 2 AND [ExportPlanId] IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_TaskChangeProposals_SourceDiseaseReportId",
                table: "TaskChangeProposals",
                column: "SourceDiseaseReportId");
        }
    }
}
