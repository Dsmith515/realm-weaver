using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealmWeaver.Modules.Rituals.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialRituals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "rituals");

            migrationBuilder.CreateTable(
                name: "rituals",
                schema: "rituals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    request_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    needle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    thread_id = table.Column<Guid>(type: "uuid", nullable: false),
                    coating_id = table.Column<Guid>(type: "uuid", nullable: false),
                    map_id = table.Column<Guid>(type: "uuid", nullable: false),
                    map_created = table.Column<bool>(type: "boolean", nullable: false),
                    realm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_mismatch = table.Column<bool>(type: "boolean", nullable: false),
                    danger_modifier = table.Column<int>(type: "integer", nullable: false),
                    encounter_variant = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rituals", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_rituals_player_id_created_at",
                schema: "rituals",
                table: "rituals",
                columns: new[] { "player_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ux_rituals_idempotency_key",
                schema: "rituals",
                table: "rituals",
                column: "idempotency_key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rituals",
                schema: "rituals");
        }
    }
}
