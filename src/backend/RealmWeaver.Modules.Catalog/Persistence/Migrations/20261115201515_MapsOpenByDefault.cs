using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealmWeaver.Modules.Catalog.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MapsOpenByDefault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Model only: MapRequired inside the map jsonb is no longer nullable and defaults to false (ADR 0010).
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
