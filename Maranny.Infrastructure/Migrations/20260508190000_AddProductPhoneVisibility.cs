using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maranny.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductPhoneVisibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ShowPhoneNumber",
                table: "Products",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShowPhoneNumber",
                table: "Products");
        }
    }
}
