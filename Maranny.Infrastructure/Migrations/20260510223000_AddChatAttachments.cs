using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maranny.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChatAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.ChatMessages', 'AttachmentUrl') IS NULL
BEGIN
    ALTER TABLE dbo.ChatMessages ADD AttachmentUrl nvarchar(500) NULL;
END

IF COL_LENGTH('dbo.ChatMessages', 'Latitude') IS NULL
BEGIN
    ALTER TABLE dbo.ChatMessages ADD Latitude float NULL;
END

IF COL_LENGTH('dbo.ChatMessages', 'Longitude') IS NULL
BEGIN
    ALTER TABLE dbo.ChatMessages ADD Longitude float NULL;
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.ChatMessages', 'Longitude') IS NOT NULL
BEGIN
    ALTER TABLE dbo.ChatMessages DROP COLUMN Longitude;
END

IF COL_LENGTH('dbo.ChatMessages', 'Latitude') IS NOT NULL
BEGIN
    ALTER TABLE dbo.ChatMessages DROP COLUMN Latitude;
END

IF COL_LENGTH('dbo.ChatMessages', 'AttachmentUrl') IS NOT NULL
BEGIN
    ALTER TABLE dbo.ChatMessages DROP COLUMN AttachmentUrl;
END");
        }
    }
}
