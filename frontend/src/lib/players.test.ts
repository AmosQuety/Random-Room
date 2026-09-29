import { describe, expect, it } from "vitest";
import { describePlayerRange, playerCountIssue } from "./players";

const game = { name: "Codenames", minPlayers: 4, maxPlayers: 10 };

describe("playerCountIssue", () => {
  it("accepts a count inside the game's range", () => {
    expect(playerCountIssue(4, game)).toBeNull();
    expect(playerCountIssue(10, game)).toBeNull();
  });

  it("asks for more players when below the minimum", () => {
    expect(playerCountIssue(2, game)).toBe("Codenames needs at least 4 players. Add 2 more.");
  });

  it("asks to remove players when above the maximum", () => {
    expect(playerCountIssue(12, game)).toBe("Codenames works best with up to 10 players. Remove 2.");
  });

  it("never allows fewer than two players or more than twelve, whatever the game declares", () => {
    const loose = { name: "Anything", minPlayers: 1, maxPlayers: 99 };
    expect(playerCountIssue(1, loose)).toContain("at least 2");
    expect(playerCountIssue(13, loose)).toContain("up to 12");
  });
});

describe("describePlayerRange", () => {
  it("summarises the range", () => {
    expect(describePlayerRange(game)).toBe("4-10 players");
  });
});
