using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealmWeaver.Modules.Catalog.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MapRecipeSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfill the recipe snapshot (ADR 0006) from the components each existing map references.
            // Their current values are the best record left of what the map was woven from.
            migrationBuilder.Sql(
                """
                UPDATE catalog.component_definitions AS m
                SET map = m.map || jsonb_build_object('Recipe', jsonb_build_object(
                    'Kind', c.coating -> 'RealmKind',
                    'FloorCount', c.coating -> 'FloorCount',
                    'Biome', t.thread -> 'Biome',
                    'Element', t.thread -> 'Element',
                    'MobTags', c.coating -> 'MobTypes',
                    'Difficulty', n.needle -> 'DifficultyTier'))
                FROM catalog.component_definitions AS n,
                     catalog.component_definitions AS t,
                     catalog.component_definitions AS c
                WHERE m.type = 'Map'
                  AND NOT (m.map ? 'Recipe')
                  AND n.id = (m.map ->> 'NeedleId')::uuid
                  AND t.id = (m.map ->> 'ThreadId')::uuid
                  AND c.id = (m.map ->> 'CoatingId')::uuid;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE catalog.component_definitions SET map = map - 'Recipe' - 'Threat' WHERE type = 'Map';");
        }
    }
}
