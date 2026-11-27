using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gearnix.Api.Data.Migrations
{
    /// <summary>
    /// Index for the storefront brand filter, which compares <c>lower(brand)</c> (Workflow.md §6.6). EF Core cannot
    /// describe an index on an expression, so it is written in SQL and is not part of the model snapshot.
    /// </summary>
    public partial class AddProductBrandIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE INDEX ix_products_brand_lower ON products (lower(brand));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX ix_products_brand_lower;");
        }
    }
}
