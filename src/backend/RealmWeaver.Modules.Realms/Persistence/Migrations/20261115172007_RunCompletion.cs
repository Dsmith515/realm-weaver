using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealmWeaver.Modules.Realms.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RunCompletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.AddColumn<bool>(
                name: "map_recorded",
                schema: "realms",
                table: "realms",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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
                name: "map_recorded",
                schema: "realms",
                table: "realms");
        }
    }
}
