using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RandomRoom.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomHostPlays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HostPlays",
                table: "Rooms",
                type: "boolean",
                nullable: false,
                // Rooms that exist already keep a host who plays. The model sets no default, so EF always writes the value.
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HostPlays",
                table: "Rooms");
        }
    }
}
