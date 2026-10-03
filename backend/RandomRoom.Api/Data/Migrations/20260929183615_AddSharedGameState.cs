using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RandomRoom.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSharedGameState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GameEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Round = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Player = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Seq = table.Column<int>(type: "integer", nullable: false),
                    ValueJson = table.Column<string>(type: "jsonb", nullable: false),
                    ServerTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Ordinal = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameEntries_GameSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "GameSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GameSessionStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Phase = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Round = table.Column<int>(type: "integer", nullable: false),
                    DeadlineAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Claimant = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    DataJson = table.Column<string>(type: "jsonb", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameSessionStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameSessionStates_GameSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "GameSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RoomGameSetups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    Json = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomGameSetups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoomGameSetups_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GameEntries_SessionId_Kind_Ordinal",
                table: "GameEntries",
                columns: new[] { "SessionId", "Kind", "Ordinal" });

            migrationBuilder.CreateIndex(
                name: "IX_GameEntries_SessionId_Round_Kind_Player_Seq",
                table: "GameEntries",
                columns: new[] { "SessionId", "Round", "Kind", "Player", "Seq" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameSessionStates_SessionId",
                table: "GameSessionStates",
                column: "SessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoomGameSetups_RoomId",
                table: "RoomGameSetups",
                column: "RoomId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GameEntries");

            migrationBuilder.DropTable(
                name: "GameSessionStates");

            migrationBuilder.DropTable(
                name: "RoomGameSetups");
        }
    }
}
