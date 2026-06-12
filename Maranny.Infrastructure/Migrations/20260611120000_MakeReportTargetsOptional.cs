using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maranny.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MakeReportTargetsOptional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Reports', 'ProductID') IS NOT NULL
BEGIN
    ALTER TABLE dbo.Reports ALTER COLUMN ProductID int NULL;
END

IF COL_LENGTH('dbo.Reports', 'CoachID') IS NOT NULL
BEGIN
    ALTER TABLE dbo.Reports ALTER COLUMN CoachID int NULL;
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Reports', 'ProductID') IS NOT NULL
BEGIN
    UPDATE dbo.Reports SET ProductID = 0 WHERE ProductID IS NULL;
    ALTER TABLE dbo.Reports ALTER COLUMN ProductID int NOT NULL;
END

IF COL_LENGTH('dbo.Reports', 'CoachID') IS NOT NULL
BEGIN
    UPDATE dbo.Reports SET CoachID = 0 WHERE CoachID IS NULL;
    ALTER TABLE dbo.Reports ALTER COLUMN CoachID int NOT NULL;
END");
        }
    }
}
