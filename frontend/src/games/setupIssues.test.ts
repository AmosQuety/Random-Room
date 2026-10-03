import { describe, expect, it } from "vitest";
import { forbiddenWordsModule } from "./forbidden-words";
import { fortunatelyModule } from "./fortunately";
import { oneWordStoryModule } from "./one-word-story";
import { randomPickerModule } from "./random-picker";
import { sketchGuessModule } from "./sketch-guess";
import { wouldYouRatherModule } from "./would-you-rather";

/** A setup that is ready must say nothing; one that is not must say what is missing, not just "finish the setup". */
describe("setup issues", () => {
  describe("round games (Would You Rather)", () => {
    const { defaultSetup, setupIssue } = wouldYouRatherModule;

    it("is silent for a ready setup", () => {
      expect(setupIssue?.(defaultSetup)).toBeNull();
    });

    it("asks for a prompt when there are no built-ins and nothing typed", () => {
      expect(setupIssue?.({ ...defaultSetup, useBuiltIn: false, custom: [{ a: "", b: "" }] })).toBe(
        "Add at least one prompt, or turn on the built-in ones.",
      );
    });

    it("names the missing half of a started dilemma", () => {
      expect(setupIssue?.({ ...defaultSetup, custom: [{ a: "Pizza", b: "" }] })).toMatch(/both options/i);
    });
  });

  describe("story games", () => {
    it.each([fortunatelyModule, oneWordStoryModule])("$name explains what is missing when no starter is chosen", (module) => {
      const issue = module.setupIssue?.({ ...module.defaultSetup, useBuiltIn: false, openers: [""] });
      expect(issue).toBe("Add at least one story starter, or turn on the built-in ones.");
    });

    it.each([fortunatelyModule, oneWordStoryModule])("$name is silent when a starter is typed", (module) => {
      expect(module.setupIssue?.({ ...module.defaultSetup, useBuiltIn: false, openers: ["Once upon a time"] })).toBeNull();
    });
  });

  describe("Forbidden Words", () => {
    const { defaultSetup, setupIssue } = forbiddenWordsModule;
    const card = (word: string, forbidden = "a, b") => ({ word, forbidden });

    it("passes on the card's own problem", () => {
      expect(setupIssue?.({ ...defaultSetup, custom: [card("Pizza", "")] })).toMatch(/at least one forbidden word/i);
    });

    it("catches two cards for the same word before the last step, ignoring case and punctuation like the server does", () => {
      expect(setupIssue?.({ ...defaultSetup, custom: [card("Dog"), card("dog!")] })).toBe("Each card needs a different word.");
      expect(forbiddenWordsModule.isSetupValid({ ...defaultSetup, custom: [card("Dog"), card("dog!")] })).toBe(false);
    });

    it("lets different cards through", () => {
      expect(setupIssue?.({ ...defaultSetup, custom: [card("Dog"), card("Cat")] })).toBeNull();
    });
  });

  describe("Sketch Guess", () => {
    const { defaultSetup, setupIssue } = sketchGuessModule;

    it("names a word that has no letters or numbers", () => {
      expect(setupIssue?.({ ...defaultSetup, words: ["bike", "!!!"] })).toBe("Word 2 needs letters or numbers.");
    });

    it("names duplicate words, ignoring case", () => {
      expect(setupIssue?.({ ...defaultSetup, words: ["Bike", "bike"] })).toBe("Each word must be different.");
    });

    it("asks for a word when the built-ins are off", () => {
      expect(setupIssue?.({ ...defaultSetup, useBuiltIn: false, words: [""] })).toBe("Add at least one word, or turn on the built-in ones.");
    });
  });

  describe("Random Picker", () => {
    it("asks for two different choices", () => {
      expect(randomPickerModule.setupIssue?.(["Same", "Same"])).toBe("Add at least two different choices.");
      expect(randomPickerModule.setupIssue?.(["Pizza", "Tacos"])).toBeNull();
    });
  });
});
