using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealmWeaver.Modules.Realms.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RepeatableCompletions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "completed_at",
                schema: "realms",
                table: "realms");

            migrationBuilder.DropColumn(
                name: "completed_objective",
                schema: "realms",
                table: "realms");

            migrationBuilder.DropColumn(
                name: "map_given_to",
                schema: "realms",
                table: "realms");

            migrationBuilder.CreateTable(
                name: "completions",
                schema: "realms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    realm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    map_component_id = table.Column<Guid>(type: "uuid", nullable: false),
                    objective = table.Column<string>(type: "jsonb", nullable: false),
                    players = table.Column<string>(type: "jsonb", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_completions", x => x.id);
                    table.ForeignKey(
                        name: "fk_completions_realms_realm_id",
                        column: x => x.realm_id,
                        principalSchema: "realms",
                        principalTable: "realms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_completions_map_component_id_completed_at",
                schema: "realms",
                table: "completions",
                columns: new[] { "map_component_id", "completed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_completions_realm_id",
                schema: "realms",
                table: "completions",
                column: "realm_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "completions",
                schema: "realms");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "completed_at",
                schema: "realms",
                table: "realms",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "completed_objective",
                schema: "realms",
                table: "realms",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<List<Guid>>(
                name: "map_given_to",
                schema: "realms",
                table: "realms",
                type: "uuid[]",
                nullable: false,
                defaultValueSql: "'{}'::uuid[]");
        }
    }
}
