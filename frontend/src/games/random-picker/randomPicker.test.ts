import { describe, expect, it } from "vitest";
import { randomPickerModule } from ".";

describe("Random Picker setup", () => {
  const valid = randomPickerModule.isSetupValid;

  it("needs two different choices, as the server counts them", () => {
    expect(valid(["Pizza", "Tacos"])).toBe(true);
    expect(valid(["Pizza", ""])).toBe(false);
    expect(valid(["Same", "Same"])).toBe(false);
    expect(valid(["Same", " Same "])).toBe(false);
  });

  it("treats a difference in capitals as a different choice, like the server does", () => {
    expect(valid(["Pizza", "pizza"])).toBe(true);
  });
});
