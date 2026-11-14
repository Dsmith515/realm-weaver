using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealmWeaver.Modules.Realms.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RealmComponentNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "coating_id",
                schema: "realms",
                table: "realms",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "coating_name",
                schema: "realms",
                table: "realms",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "map_name",
                schema: "realms",
                table: "realms",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "needle_id",
                schema: "realms",
                table: "realms",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "needle_name",
                schema: "realms",
                table: "realms",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "thread_id",
                schema: "realms",
                table: "realms",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "thread_name",
                schema: "realms",
                table: "realms",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "coating_id",
                schema: "realms",
                table: "realms");

            migrationBuilder.DropColumn(
                name: "coating_name",
                schema: "realms",
                table: "realms");

            migrationBuilder.DropColumn(
                name: "map_name",
                schema: "realms",
                table: "realms");

            migrationBuilder.DropColumn(
                name: "needle_id",
                schema: "realms",
                table: "realms");

            migrationBuilder.DropColumn(
                name: "needle_name",
                schema: "realms",
                table: "realms");

            migrationBuilder.DropColumn(
                name: "thread_id",
                schema: "realms",
                table: "realms");

            migrationBuilder.DropColumn(
                name: "thread_name",
                schema: "realms",
                table: "realms");
        }
    }
}
