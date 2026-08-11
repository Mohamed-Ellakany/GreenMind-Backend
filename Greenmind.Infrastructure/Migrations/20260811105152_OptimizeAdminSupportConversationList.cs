using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Greenmind.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeAdminSupportConversationList : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_SupportConversations_LastMessageAtUtc_Id",
                table: "SupportConversations",
                columns: new[] { "LastMessageAtUtc", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SupportConversations_LastMessageAtUtc_Id",
                table: "SupportConversations");
        }
    }
}
