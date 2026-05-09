using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maranny.Infrastructure.Migrations
{
    /// <inheritdoc />
    [Migration("20260509120000_AddCoachAge")]
    public partial class AddCoachAge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Coaches', 'Age') IS NULL
BEGIN
    ALTER TABLE dbo.Coaches ADD Age int NULL;
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Coaches', 'Age') IS NOT NULL
BEGIN
    ALTER TABLE dbo.Coaches DROP COLUMN Age;
END");
        }
    }
}
