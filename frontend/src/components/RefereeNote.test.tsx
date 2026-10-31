import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { RefereeNote } from "./RefereeNote";

describe("RefereeNote", () => {
  it("says the host runs the game and does not play", () => {
    render(<RefereeNote />);

    expect(screen.getByRole("region", { name: "You are refereeing" })).toHaveTextContent("You run the game and do not play.");
  });

  it("adds what the referee can do here", () => {
    render(<RefereeNote>Reveal each answer when you are ready.</RefereeNote>);

    expect(screen.getByRole("region", { name: "You are refereeing" })).toHaveTextContent("Reveal each answer when you are ready.");
  });
});
