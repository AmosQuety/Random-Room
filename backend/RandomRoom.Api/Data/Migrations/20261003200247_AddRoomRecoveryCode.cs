using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RandomRoom.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomRecoveryCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RecoveryCodeHash",
                table: "Rooms",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RecoveryCodeHash",
                table: "Rooms");
        }
    }
}
