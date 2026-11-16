using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealmWeaver.Modules.Realms.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MapHandOut : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "map_recorded",
                schema: "realms",
                table: "realms");

            migrationBuilder.AddColumn<List<Guid>>(
                name: "map_given_to",
                schema: "realms",
                table: "realms",
                type: "uuid[]",
                nullable: false,
                defaultValueSql: "'{}'::uuid[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "map_given_to",
                schema: "realms",
                table: "realms");

            migrationBuilder.AddColumn<bool>(
                name: "map_recorded",
                schema: "realms",
                table: "realms",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
