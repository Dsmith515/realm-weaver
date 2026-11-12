using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealmWeaver.Modules.Inventory.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "inventory");

            migrationBuilder.CreateTable(
                name: "players",
                schema: "inventory",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_players", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "inventory_items",
                schema: "inventory",
                columns: table => new
                {
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    component_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    acquired_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventory_items", x => new { x.player_id, x.component_definition_id });
                    table.CheckConstraint("ck_inventory_items_quantity", "quantity >= 0");
                    table.ForeignKey(
                        name: "fk_inventory_items_players_player_id",
                        column: x => x.player_id,
                        principalSchema: "inventory",
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_inventory_items_component_definition_id",
                schema: "inventory",
                table: "inventory_items",
                column: "component_definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_players_name",
                schema: "inventory",
                table: "players",
                column: "name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inventory_items",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "players",
                schema: "inventory");
        }
    }
}
