using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Greenmind.Infrastructure.Migrations;

[DbContext(typeof(Persistence.GreenmindDbContext))]
[Migration("20260801190000_AddSerilogLogs")]
public partial class AddSerilogLogs : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Logs",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Message = table.Column<string>(type: "nvarchar(max)", nullable: true),
                MessageTemplate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                Level = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                TimeStamp = table.Column<DateTime>(type: "datetime", nullable: true),
                Exception = table.Column<string>(type: "nvarchar(max)", nullable: true),
                LogEvent = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Logs", item => item.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Logs_TimeStamp",
            table: "Logs",
            column: "TimeStamp");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Logs");
    }
}
