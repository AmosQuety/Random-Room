import { describe, expect, it } from "vitest";
import { GAME_LIST } from "./registry";
import {
  MAX_CATEGORY_LENGTH,
  MAX_OPTIONS,
  MAX_OPTION_LENGTH,
  MAX_QUESTIONS,
  MAX_QUESTION_LENGTH,
  STARTER_PACKS,
} from "./trivia/setup";

/**
 * The server's own numbers, written by backend ServerFactsTests (and kept current by it). These tests compare the
 * frontend's copies against them, and fail rather than skip when there is nothing to compare.
 */
interface ServerFacts {
  games: { key: string; minPlayers: number; maxPlayers: number }[];
  trivia: {
    maxQuestions: number;
    maxQuestionLength: number;
    maxOptionsPerQuestion: number;
    maxOptionLength: number;
    maxCategoryLength: number;
    starterPackSizes: Record<string, number>;
  };
}

const FIXTURE = "./__fixtures__/server-facts.json";

function loadServerFacts(): ServerFacts {
  const found = import.meta.glob("./__fixtures__/server-facts.json", { eager: true, import: "default" });
  const raw = found[FIXTURE] as Partial<ServerFacts> | undefined;
  if (!raw) throw new Error(`${FIXTURE} is missing. Run the backend ServerFactsTests with UPDATE_SERVER_FACTS=1 to create it.`);
  if (!Array.isArray(raw.games) || raw.games.length === 0) throw new Error(`${FIXTURE} lists no games, so nothing can be compared.`);
  if (!raw.trivia || Object.keys(raw.trivia.starterPackSizes ?? {}).length === 0) throw new Error(`${FIXTURE} has no trivia facts, so nothing can be compared.`);
  return raw as ServerFacts;
}

const server = loadServerFacts();
const byKey = <T extends { key: string }>(items: T[]) => [...items].sort((a, b) => (a.key < b.key ? -1 : a.key > b.key ? 1 : 0));

describe("server facts the frontend keeps a copy of", () => {
  it("has exactly the games the server registers", () => {
    const serverKeys = byKey(server.games).map((g) => g.key);
    const clientKeys = byKey(GAME_LIST).map((g) => g.key);

    expect(clientKeys).toEqual(serverKeys);
  });

  it("uses the server's player range for every game", () => {
    const serverRanges = byKey(server.games).map(({ key, minPlayers, maxPlayers }) => ({ key, minPlayers, maxPlayers }));
    const clientRanges = byKey(GAME_LIST).map(({ key, minPlayers, maxPlayers }) => ({ key, minPlayers, maxPlayers }));

    expect(clientRanges).toEqual(serverRanges);
  });

  it("uses the server's trivia limits", () => {
    const clientLimits = {
      maxQuestions: MAX_QUESTIONS,
      maxQuestionLength: MAX_QUESTION_LENGTH,
      maxOptionsPerQuestion: MAX_OPTIONS,
      maxOptionLength: MAX_OPTION_LENGTH,
      maxCategoryLength: MAX_CATEGORY_LENGTH,
    };
    const { starterPackSizes: _packs, ...serverLimits } = server.trivia;

    expect(clientLimits).toEqual(serverLimits);
  });

  it("offers the server's starter packs at the server's sizes", () => {
    const clientPacks = Object.fromEntries(STARTER_PACKS.map((p) => [p.key, p.size]));

    expect(clientPacks).toEqual(server.trivia.starterPackSizes);
  });
});
