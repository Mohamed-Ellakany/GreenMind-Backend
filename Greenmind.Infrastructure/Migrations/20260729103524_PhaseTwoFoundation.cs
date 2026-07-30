using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Greenmind.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PhaseTwoFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DiseaseReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExportPlanId = table.Column<int>(type: "int", nullable: false),
                    ObservationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AffectedArea = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UserNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SuspectedDisease = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ConfidencePercentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    Severity = table.Column<int>(type: "int", nullable: true),
                    ObservedSymptoms = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RecommendedActions = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExportImpact = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AdvisoryWarning = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ModelName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PromptVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    FailureMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiseaseReports", x => x.Id);
                    table.CheckConstraint("CK_DiseaseReports_Confidence", "[ConfidencePercentage] IS NULL OR ([ConfidencePercentage] >= 0 AND [ConfidencePercentage] <= 100)");
                    table.ForeignKey(
                        name: "FK_DiseaseReports_ExportPlans_ExportPlanId",
                        column: x => x.ExportPlanId,
                        principalTable: "ExportPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExpenseCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FarmId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpenseCategories_Farms_FarmId",
                        column: x => x.FarmId,
                        principalTable: "Farms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExportPlanBudgets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExportPlanId = table.Column<int>(type: "int", nullable: false),
                    TotalBudget = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExportPlanBudgets", x => x.Id);
                    table.CheckConstraint("CK_ExportPlanBudgets_TotalBudget", "[TotalBudget] IS NULL OR [TotalBudget] >= 0");
                    table.ForeignKey(
                        name: "FK_ExportPlanBudgets_ExportPlans_ExportPlanId",
                        column: x => x.ExportPlanId,
                        principalTable: "ExportPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DiseaseImages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DiseaseReportId = table.Column<int>(type: "int", nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    Checksum = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiseaseImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiseaseImages_DiseaseReports_DiseaseReportId",
                        column: x => x.DiseaseReportId,
                        principalTable: "DiseaseReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Expenses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExportPlanId = table.Column<int>(type: "int", nullable: false),
                    ExpenseCategoryId = table.Column<int>(type: "int", nullable: false),
                    TaskId = table.Column<int>(type: "int", nullable: true),
                    ExpenseDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Supplier = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentMethod = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Expenses", x => x.Id);
                    table.CheckConstraint("CK_Expenses_Amount", "[Amount] >= 0");
                    table.CheckConstraint("CK_Expenses_Quantity", "[Quantity] > 0");
                    table.CheckConstraint("CK_Expenses_UnitPrice", "[UnitPrice] >= 0");
                    table.ForeignKey(
                        name: "FK_Expenses_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Expenses_AspNetUsers_DeletedByUserId",
                        column: x => x.DeletedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Expenses_AspNetUsers_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Expenses_ExpenseCategories_ExpenseCategoryId",
                        column: x => x.ExpenseCategoryId,
                        principalTable: "ExpenseCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Expenses_ExportPlans_ExportPlanId",
                        column: x => x.ExportPlanId,
                        principalTable: "ExportPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Expenses_FarmTasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "FarmTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExportPlanBudgetLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BudgetId = table.Column<int>(type: "int", nullable: false),
                    ExpenseCategoryId = table.Column<int>(type: "int", nullable: false),
                    PlannedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExportPlanBudgetLines", x => x.Id);
                    table.CheckConstraint("CK_ExportPlanBudgetLines_PlannedAmount", "[PlannedAmount] >= 0");
                    table.ForeignKey(
                        name: "FK_ExportPlanBudgetLines_ExpenseCategories_ExpenseCategoryId",
                        column: x => x.ExpenseCategoryId,
                        principalTable: "ExpenseCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExportPlanBudgetLines_ExportPlanBudgets_BudgetId",
                        column: x => x.BudgetId,
                        principalTable: "ExportPlanBudgets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExpenseAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExpenseId = table.Column<int>(type: "int", nullable: false),
                    AttachmentType = table.Column<int>(type: "int", nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    Checksum = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    UploadedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpenseAttachments_AspNetUsers_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExpenseAttachments_Expenses_ExpenseId",
                        column: x => x.ExpenseId,
                        principalTable: "Expenses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChatAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChatMessageId = table.Column<int>(type: "int", nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    Checksum = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatAttachments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChatMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChatSessionId = table.Column<int>(type: "int", nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ContextReferencesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModelName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    InputTokens = table.Column<int>(type: "int", nullable: true),
                    OutputTokens = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChatSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FarmId = table.Column<int>(type: "int", nullable: false),
                    ExportPlanId = table.Column<int>(type: "int", nullable: true),
                    Scope = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SummaryThroughMessageId = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatSessions", x => x.Id);
                    table.CheckConstraint("CK_ChatSessions_Scope", "([Scope] = 1 AND [ExportPlanId] IS NOT NULL) OR ([Scope] = 2 AND [ExportPlanId] IS NULL)");
                    table.ForeignKey(
                        name: "FK_ChatSessions_ChatMessages_SummaryThroughMessageId",
                        column: x => x.SummaryThroughMessageId,
                        principalTable: "ChatMessages",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ChatSessions_ExportPlans_ExportPlanId",
                        column: x => x.ExportPlanId,
                        principalTable: "ExportPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChatSessions_Farms_FarmId",
                        column: x => x.FarmId,
                        principalTable: "Farms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TaskChangeProposals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExportPlanId = table.Column<int>(type: "int", nullable: false),
                    SourceChatMessageId = table.Column<int>(type: "int", nullable: true),
                    SourceDiseaseReportId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    FailureMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskChangeProposals", x => x.Id);
                    table.CheckConstraint("CK_TaskChangeProposals_Source", "NOT ([SourceChatMessageId] IS NOT NULL AND [SourceDiseaseReportId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_TaskChangeProposals_AspNetUsers_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaskChangeProposals_ChatMessages_SourceChatMessageId",
                        column: x => x.SourceChatMessageId,
                        principalTable: "ChatMessages",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TaskChangeProposals_DiseaseReports_SourceDiseaseReportId",
                        column: x => x.SourceDiseaseReportId,
                        principalTable: "DiseaseReports",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TaskChangeProposals_ExportPlans_ExportPlanId",
                        column: x => x.ExportPlanId,
                        principalTable: "ExportPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaskChangeProposalItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TaskChangeProposalId = table.Column<int>(type: "int", nullable: false),
                    Operation = table.Column<int>(type: "int", nullable: false),
                    FarmTaskId = table.Column<int>(type: "int", nullable: true),
                    OriginalValuesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProposedValuesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskChangeProposalItems", x => x.Id);
                    table.CheckConstraint("CK_TaskChangeProposalItems_Target", "([Operation] = 1 AND [FarmTaskId] IS NULL) OR ([Operation] IN (2, 3) AND [FarmTaskId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_TaskChangeProposalItems_FarmTasks_FarmTaskId",
                        column: x => x.FarmTaskId,
                        principalTable: "FarmTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaskChangeProposalItems_TaskChangeProposals_TaskChangeProposalId",
                        column: x => x.TaskChangeProposalId,
                        principalTable: "TaskChangeProposals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatAttachments_ChatMessageId",
                table: "ChatAttachments",
                column: "ChatMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatAttachments_StorageKey",
                table: "ChatAttachments",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_ChatSessionId_CreatedAt_Id",
                table: "ChatMessages",
                columns: new[] { "ChatSessionId", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_ChatSessionId_IdempotencyKey",
                table: "ChatMessages",
                columns: new[] { "ChatSessionId", "IdempotencyKey" },
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ChatSessions_ExportPlanId_CreatedAt",
                table: "ChatSessions",
                columns: new[] { "ExportPlanId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatSessions_FarmId_Scope_IsActive",
                table: "ChatSessions",
                columns: new[] { "FarmId", "Scope", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatSessions_SummaryThroughMessageId",
                table: "ChatSessions",
                column: "SummaryThroughMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_DiseaseImages_DiseaseReportId",
                table: "DiseaseImages",
                column: "DiseaseReportId");

            migrationBuilder.CreateIndex(
                name: "IX_DiseaseImages_StorageKey",
                table: "DiseaseImages",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiseaseReports_ExportPlanId_ObservationDate_CreatedAt",
                table: "DiseaseReports",
                columns: new[] { "ExportPlanId", "ObservationDate", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DiseaseReports_Status_CreatedAt",
                table: "DiseaseReports",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseAttachments_ExpenseId",
                table: "ExpenseAttachments",
                column: "ExpenseId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseAttachments_StorageKey",
                table: "ExpenseAttachments",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseAttachments_UploadedByUserId",
                table: "ExpenseAttachments",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseCategories_FarmId_Name",
                table: "ExpenseCategories",
                columns: new[] { "FarmId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_CreatedByUserId",
                table: "Expenses",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_DeletedByUserId",
                table: "Expenses",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_ExpenseCategoryId_IsDeleted",
                table: "Expenses",
                columns: new[] { "ExpenseCategoryId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_ExportPlanId_IsDeleted_ExpenseDate",
                table: "Expenses",
                columns: new[] { "ExportPlanId", "IsDeleted", "ExpenseDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_TaskId",
                table: "Expenses",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_UpdatedByUserId",
                table: "Expenses",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ExportPlanBudgetLines_BudgetId_ExpenseCategoryId",
                table: "ExportPlanBudgetLines",
                columns: new[] { "BudgetId", "ExpenseCategoryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExportPlanBudgetLines_ExpenseCategoryId",
                table: "ExportPlanBudgetLines",
                column: "ExpenseCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ExportPlanBudgets_ExportPlanId",
                table: "ExportPlanBudgets",
                column: "ExportPlanId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskChangeProposalItems_FarmTaskId",
                table: "TaskChangeProposalItems",
                column: "FarmTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskChangeProposalItems_TaskChangeProposalId",
                table: "TaskChangeProposalItems",
                column: "TaskChangeProposalId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskChangeProposals_ExportPlanId_Status_ExpiresAtUtc",
                table: "TaskChangeProposals",
                columns: new[] { "ExportPlanId", "Status", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskChangeProposals_ReviewedByUserId",
                table: "TaskChangeProposals",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskChangeProposals_SourceChatMessageId",
                table: "TaskChangeProposals",
                column: "SourceChatMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskChangeProposals_SourceDiseaseReportId",
                table: "TaskChangeProposals",
                column: "SourceDiseaseReportId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatAttachments_ChatMessages_ChatMessageId",
                table: "ChatAttachments",
                column: "ChatMessageId",
                principalTable: "ChatMessages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_ChatSessions_ChatSessionId",
                table: "ChatMessages",
                column: "ChatSessionId",
                principalTable: "ChatSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatSessions_ChatMessages_SummaryThroughMessageId",
                table: "ChatSessions");

            migrationBuilder.DropTable(
                name: "ChatAttachments");

            migrationBuilder.DropTable(
                name: "DiseaseImages");

            migrationBuilder.DropTable(
                name: "ExpenseAttachments");

            migrationBuilder.DropTable(
                name: "ExportPlanBudgetLines");

            migrationBuilder.DropTable(
                name: "TaskChangeProposalItems");

            migrationBuilder.DropTable(
                name: "Expenses");

            migrationBuilder.DropTable(
                name: "ExportPlanBudgets");

            migrationBuilder.DropTable(
                name: "TaskChangeProposals");

            migrationBuilder.DropTable(
                name: "ExpenseCategories");

            migrationBuilder.DropTable(
                name: "DiseaseReports");

            migrationBuilder.DropTable(
                name: "ChatMessages");

            migrationBuilder.DropTable(
                name: "ChatSessions");
        }
    }
}
