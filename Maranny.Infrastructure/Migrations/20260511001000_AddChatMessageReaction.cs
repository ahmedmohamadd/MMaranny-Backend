using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maranny.Infrastructure.Migrations
{
    public partial class AddChatMessageReaction : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.ChatMessages', 'Reaction') IS NULL
BEGIN
    ALTER TABLE dbo.ChatMessages ADD Reaction nvarchar(20) NULL;
END
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.ChatMessages', 'Reaction') IS NOT NULL
BEGIN
    ALTER TABLE dbo.ChatMessages DROP COLUMN Reaction;
END
");
        }
    }
}
