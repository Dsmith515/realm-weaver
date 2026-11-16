using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealmWeaver.Modules.Inventory.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AttunementAndSkills : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "attuned_needle_id",
                schema: "inventory",
                table: "players",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "trained_skills",
                schema: "inventory",
                table: "players",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'::text[]");

            // Existing players attune the beginner needle when they own it, as new players do.
            migrationBuilder.Sql(
                """
                UPDATE inventory.players p SET attuned_needle_id = '10000000-0000-0000-0000-000000000001'
                WHERE EXISTS (
                    SELECT 1 FROM inventory.inventory_items i
                    WHERE i.player_id = p.id AND i.component_definition_id = '10000000-0000-0000-0000-000000000001' AND i.quantity > 0)
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "attuned_needle_id",
                schema: "inventory",
                table: "players");

            migrationBuilder.DropColumn(
                name: "trained_skills",
                schema: "inventory",
                table: "players");
        }
    }
}
