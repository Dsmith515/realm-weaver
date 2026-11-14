using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealmWeaver.Modules.Catalog.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MapRecipeSettlements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Map recipes live in the jsonb "map" column, so the new SettlementPopulation and IncludeStructures fields
            // need no schema change. This migration only records them in the model snapshot.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
