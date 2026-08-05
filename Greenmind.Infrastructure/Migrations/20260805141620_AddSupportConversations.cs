using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Greenmind.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSupportConversations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SupportConversations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FarmId = table.Column<int>(type: "int", nullable: false),
                    LastMessageAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastAdminReplyAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastCustomerMessageAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsClosed = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportConversations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupportConversations_Farms_FarmId",
                        column: x => x.FarmId,
                        principalTable: "Farms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupportMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SupportConversationId = table.Column<int>(type: "int", nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false),
                    SenderUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupportMessages_AspNetUsers_SenderUserId",
                        column: x => x.SenderUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupportMessages_SupportConversations_SupportConversationId",
                        column: x => x.SupportConversationId,
                        principalTable: "SupportConversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupportConversations_FarmId_IsClosed_LastMessageAtUtc",
                table: "SupportConversations",
                columns: new[] { "FarmId", "IsClosed", "LastMessageAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportMessages_SenderUserId",
                table: "SupportMessages",
                column: "SenderUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportMessages_SupportConversationId_CreatedAt_Id",
                table: "SupportMessages",
                columns: new[] { "SupportConversationId", "CreatedAt", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SupportMessages");

            migrationBuilder.DropTable(
                name: "SupportConversations");
        }
    }
}
