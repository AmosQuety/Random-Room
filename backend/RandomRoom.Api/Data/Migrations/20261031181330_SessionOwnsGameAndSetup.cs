using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RandomRoom.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SessionOwnsGameAndSetup : Migration
    {
        // Every session gets its room's game type. The latest session of each room takes the room's existing setup rows
        // (so the game in play keeps the very rows it uses); every older session gets copies with new ids. A trivia
        // answer is repointed to its own session's copy of the question, so scores and reveals are unchanged.
        private const string BackfillUp = """
            UPDATE "GameSessions" AS s SET "GameType" = r."GameType" FROM "Rooms" AS r WHERE r."Id" = s."RoomId";

            CREATE TEMP TABLE "LatestSessions" AS
                SELECT DISTINCT ON ("RoomId") "RoomId", "Id" AS "SessionId" FROM "GameSessions" ORDER BY "RoomId", "Number" DESC;

            UPDATE "RoomGameSetups" AS x SET "SessionId" = l."SessionId" FROM "LatestSessions" AS l WHERE l."RoomId" = x."RoomId";
            UPDATE "RoomChoices" AS x SET "SessionId" = l."SessionId" FROM "LatestSessions" AS l WHERE l."RoomId" = x."RoomId";
            UPDATE "TriviaQuestions" AS x SET "SessionId" = l."SessionId" FROM "LatestSessions" AS l WHERE l."RoomId" = x."RoomId";

            INSERT INTO "RoomGameSetups" ("Id", "RoomId", "SessionId", "Json")
                SELECT gen_random_uuid(), x."RoomId", s."Id", x."Json"
                FROM "RoomGameSetups" AS x JOIN "GameSessions" AS s ON s."RoomId" = x."RoomId" AND s."Id" <> x."SessionId";

            INSERT INTO "RoomChoices" ("Id", "RoomId", "SessionId", "Label", "Position")
                SELECT gen_random_uuid(), x."RoomId", s."Id", x."Label", x."Position"
                FROM "RoomChoices" AS x JOIN "GameSessions" AS s ON s."RoomId" = x."RoomId" AND s."Id" <> x."SessionId";

            CREATE TEMP TABLE "TriviaCopies" AS
                SELECT q."Id" AS "OriginalId", s."Id" AS "SessionId", gen_random_uuid() AS "CopyId"
                FROM "TriviaQuestions" AS q JOIN "GameSessions" AS s ON s."RoomId" = q."RoomId" AND s."Id" <> q."SessionId";

            INSERT INTO "TriviaQuestions" ("Id", "RoomId", "SessionId", "Position", "Text", "Options", "CorrectIndex", "Category")
                SELECT c."CopyId", q."RoomId", c."SessionId", q."Position", q."Text", q."Options", q."CorrectIndex", q."Category"
                FROM "TriviaCopies" AS c JOIN "TriviaQuestions" AS q ON q."Id" = c."OriginalId";

            UPDATE "TriviaAnswers" AS a SET "QuestionId" = c."CopyId"
                FROM "TriviaCopies" AS c WHERE c."OriginalId" = a."QuestionId" AND c."SessionId" = a."SessionId";

            DROP TABLE "TriviaCopies";
            DROP TABLE "LatestSessions";

            ALTER TABLE "GameSessions" ALTER COLUMN "GameType" DROP DEFAULT;
            """;

        // Back to one set of setup rows per room: each room keeps the rows of its latest session that has any, and
        // older sessions' copies are deleted. Trivia answers are first repointed to the kept question at the same
        // position; an answer with no such question makes the delete fail (restrict), so the rollback stops rather
        // than lose it. Per-session game types are dropped, which is lossless only while every session plays its
        // room's game.
        private const string CollapseDown = """
            CREATE TEMP TABLE "KeptTrivia" AS
                SELECT DISTINCT ON (q."RoomId") q."RoomId", q."SessionId"
                FROM "TriviaQuestions" AS q JOIN "GameSessions" AS s ON s."Id" = q."SessionId"
                ORDER BY q."RoomId", s."Number" DESC;

            UPDATE "TriviaAnswers" AS a SET "QuestionId" = kq."Id"
                FROM "TriviaQuestions" AS cur, "KeptTrivia" AS k, "TriviaQuestions" AS kq
                WHERE cur."Id" = a."QuestionId" AND k."RoomId" = cur."RoomId" AND cur."SessionId" <> k."SessionId"
                    AND kq."SessionId" = k."SessionId" AND kq."Position" = cur."Position";

            DELETE FROM "TriviaQuestions" AS q USING "KeptTrivia" AS k WHERE k."RoomId" = q."RoomId" AND q."SessionId" <> k."SessionId";

            DELETE FROM "RoomChoices" AS x USING (
                SELECT DISTINCT ON (c."RoomId") c."RoomId", c."SessionId"
                FROM "RoomChoices" AS c JOIN "GameSessions" AS s ON s."Id" = c."SessionId" ORDER BY c."RoomId", s."Number" DESC) AS k
                WHERE k."RoomId" = x."RoomId" AND x."SessionId" <> k."SessionId";

            DELETE FROM "RoomGameSetups" AS x USING (
                SELECT DISTINCT ON (g."RoomId") g."RoomId", g."SessionId"
                FROM "RoomGameSetups" AS g JOIN "GameSessions" AS s ON s."Id" = g."SessionId" ORDER BY g."RoomId", s."Number" DESC) AS k
                WHERE k."RoomId" = x."RoomId" AND x."SessionId" <> k."SessionId";

            DROP TABLE "KeptTrivia";
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SessionId",
                table: "TriviaQuestions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SessionId",
                table: "RoomGameSetups",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SessionId",
                table: "RoomChoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GameType",
                table: "GameSessions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                // A placeholder until the backfill below, then dropped: the model sets no default, so a missing value fails.
                defaultValue: "");

            // The unique index on RoomId goes first: every session of a room is about to get its own row.
            migrationBuilder.DropIndex(
                name: "IX_RoomGameSetups_RoomId",
                table: "RoomGameSetups");

            migrationBuilder.Sql(BackfillUp);

            migrationBuilder.CreateIndex(
                name: "IX_TriviaQuestions_SessionId",
                table: "TriviaQuestions",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_RoomGameSetups_RoomId",
                table: "RoomGameSetups",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_RoomGameSetups_SessionId",
                table: "RoomGameSetups",
                column: "SessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoomChoices_SessionId",
                table: "RoomChoices",
                column: "SessionId");

            migrationBuilder.AddForeignKey(
                name: "FK_RoomChoices_GameSessions_SessionId",
                table: "RoomChoices",
                column: "SessionId",
                principalTable: "GameSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RoomGameSetups_GameSessions_SessionId",
                table: "RoomGameSetups",
                column: "SessionId",
                principalTable: "GameSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TriviaQuestions_GameSessions_SessionId",
                table: "TriviaQuestions",
                column: "SessionId",
                principalTable: "GameSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(CollapseDown);

            migrationBuilder.DropForeignKey(
                name: "FK_RoomChoices_GameSessions_SessionId",
                table: "RoomChoices");

            migrationBuilder.DropForeignKey(
                name: "FK_RoomGameSetups_GameSessions_SessionId",
                table: "RoomGameSetups");

            migrationBuilder.DropForeignKey(
                name: "FK_TriviaQuestions_GameSessions_SessionId",
                table: "TriviaQuestions");

            migrationBuilder.DropIndex(
                name: "IX_TriviaQuestions_SessionId",
                table: "TriviaQuestions");

            migrationBuilder.DropIndex(
                name: "IX_RoomGameSetups_RoomId",
                table: "RoomGameSetups");

            migrationBuilder.DropIndex(
                name: "IX_RoomGameSetups_SessionId",
                table: "RoomGameSetups");

            migrationBuilder.DropIndex(
                name: "IX_RoomChoices_SessionId",
                table: "RoomChoices");

            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "TriviaQuestions");

            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "RoomGameSetups");

            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "RoomChoices");

            migrationBuilder.DropColumn(
                name: "GameType",
                table: "GameSessions");

            migrationBuilder.CreateIndex(
                name: "IX_RoomGameSetups_RoomId",
                table: "RoomGameSetups",
                column: "RoomId",
                unique: true);
        }
    }
}
