using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Greenmind.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnsureSingleOpenSupportConversation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ;WITH RankedConversations AS
                (
                    SELECT
                        [Id],
                        FIRST_VALUE([Id]) OVER (
                            PARTITION BY [FarmId]
                            ORDER BY [LastMessageAtUtc] DESC, [Id] DESC) AS [KeepId],
                        ROW_NUMBER() OVER (
                            PARTITION BY [FarmId]
                            ORDER BY [LastMessageAtUtc] DESC, [Id] DESC) AS [RowNumber]
                    FROM [SupportConversations]
                    WHERE [IsClosed] = 0
                )
                UPDATE [Messages]
                SET [Messages].[SupportConversationId] = [Ranked].[KeepId]
                FROM [SupportMessages] AS [Messages]
                INNER JOIN [RankedConversations] AS [Ranked]
                    ON [Messages].[SupportConversationId] = [Ranked].[Id]
                WHERE [Ranked].[RowNumber] > 1;

                ;WITH RankedConversations AS
                (
                    SELECT
                        [Id],
                        ROW_NUMBER() OVER (
                            PARTITION BY [FarmId]
                            ORDER BY [LastMessageAtUtc] DESC, [Id] DESC) AS [RowNumber]
                    FROM [SupportConversations]
                    WHERE [IsClosed] = 0
                )
                DELETE [Conversations]
                FROM [SupportConversations] AS [Conversations]
                INNER JOIN [RankedConversations] AS [Ranked]
                    ON [Conversations].[Id] = [Ranked].[Id]
                WHERE [Ranked].[RowNumber] > 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_SupportConversations_FarmId",
                table: "SupportConversations",
                column: "FarmId",
                unique: true,
                filter: "[IsClosed] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SupportConversations_FarmId",
                table: "SupportConversations");
        }
    }
}
