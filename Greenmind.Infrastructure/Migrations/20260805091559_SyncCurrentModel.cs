using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Greenmind.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SyncCurrentModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Quantity",
                table: "FarmTasks",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "FarmTasks",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "وحدة");

            migrationBuilder.Sql("UPDATE [FarmTasks] SET [Quantity] = 1 WHERE [Quantity] <= 0;");
            migrationBuilder.Sql("UPDATE [FarmTasks] SET [Unit] = N'وحدة' WHERE [Unit] IS NULL OR LTRIM(RTRIM([Unit])) = N'';");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FarmTasks_Quantity",
                table: "FarmTasks",
                sql: "[Quantity] > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_FarmTasks_Quantity",
                table: "FarmTasks");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "FarmTasks");

            migrationBuilder.DropColumn(
                name: "Unit",
                table: "FarmTasks");
        }
    }
}
