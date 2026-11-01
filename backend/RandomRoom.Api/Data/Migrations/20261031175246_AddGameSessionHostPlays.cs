using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RandomRoom.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGameSessionHostPlays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HostPlays",
                table: "GameSessions",
                type: "boolean",
                nullable: false,
                // A placeholder until the backfill below; the model sets no default, so EF always writes the value.
                defaultValue: true);

            // Existing sessions take their room's current setting, which is what they were played with until now.
            migrationBuilder.Sql(
                "UPDATE \"GameSessions\" AS s SET \"HostPlays\" = r.\"HostPlays\" FROM \"Rooms\" AS r WHERE r.\"Id\" = s.\"RoomId\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HostPlays",
                table: "GameSessions");
        }
    }
}
