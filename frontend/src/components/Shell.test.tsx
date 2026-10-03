import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { Shell } from "./Shell";

describe("Shell", () => {
  // jsdom has no layout, so this only guards the class. The behaviour (a scaled-up reveal animation widening the page on
  // phones) was reproduced and fixed in a real browser with mobile emulation.
  it("clips horizontal overflow in the content area so animations cannot widen the page on phones", () => {
    render(<Shell>content</Shell>);

    expect(screen.getByRole("main")).toHaveClass("overflow-x-clip");
  });
});
