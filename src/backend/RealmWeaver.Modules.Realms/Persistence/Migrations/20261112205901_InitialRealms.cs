using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealmWeaver.Modules.Realms.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialRealms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "realms");

            migrationBuilder.CreateTable(
                name: "realms",
                schema: "realms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    map_component_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    biome = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    width = table.Column<int>(type: "integer", nullable: false),
                    height = table.Column<int>(type: "integer", nullable: false),
                    floor_count = table.Column<int>(type: "integer", nullable: false),
                    threat = table.Column<int>(type: "integer", nullable: false),
                    danger_modifier = table.Column<int>(type: "integer", nullable: false),
                    encounter_variant = table.Column<int>(type: "integer", nullable: false),
                    seed = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    generator_version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    document = table.Column<string>(type: "jsonb", nullable: false),
                    areas = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_realms", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_realms_map_component_id",
                schema: "realms",
                table: "realms",
                column: "map_component_id");

            migrationBuilder.CreateIndex(
                name: "ix_realms_player_id_created_at",
                schema: "realms",
                table: "realms",
                columns: new[] { "player_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "realms",
                schema: "realms");
        }
    }
}
