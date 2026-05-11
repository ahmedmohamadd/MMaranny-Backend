using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maranny.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductListingLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Products', 'ListingLocation') IS NULL
BEGIN
    ALTER TABLE dbo.Products ADD ListingLocation nvarchar(200) NULL;
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Products', 'ListingLocation') IS NOT NULL
BEGIN
    ALTER TABLE dbo.Products DROP COLUMN ListingLocation;
END");
        }
    }
}
