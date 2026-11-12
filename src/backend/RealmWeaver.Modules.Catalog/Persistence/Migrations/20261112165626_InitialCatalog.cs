using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealmWeaver.Modules.Catalog.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "catalog");

            migrationBuilder.CreateTable(
                name: "component_definitions",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    rarity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    rank = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    is_player_assignable = table.Column<bool>(type: "boolean", nullable: false),
                    is_consumable = table.Column<bool>(type: "boolean", nullable: false),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    coating = table.Column<string>(type: "jsonb", nullable: true),
                    map = table.Column<string>(type: "jsonb", nullable: true),
                    needle = table.Column<string>(type: "jsonb", nullable: true),
                    thread = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_component_definitions", x => x.id);
                    table.CheckConstraint("ck_component_definitions_one_details", "(CASE WHEN needle IS NULL THEN 0 ELSE 1 END + CASE WHEN thread IS NULL THEN 0 ELSE 1 END + CASE WHEN coating IS NULL THEN 0 ELSE 1 END + CASE WHEN map IS NULL THEN 0 ELSE 1 END) = 1");
                    table.CheckConstraint("ck_component_definitions_rank", "rank >= 1");
                });

            migrationBuilder.CreateTable(
                name: "settings",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    players_can_self_assign = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_settings", x => x.id);
                    table.CheckConstraint("ck_settings_singleton", "id = 1");
                });

            migrationBuilder.CreateIndex(
                name: "ix_component_definitions_name",
                schema: "catalog",
                table: "component_definitions",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "ix_component_definitions_type",
                schema: "catalog",
                table: "component_definitions",
                column: "type");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "component_definitions",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "settings",
                schema: "catalog");
        }
    }
}
