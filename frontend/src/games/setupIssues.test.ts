import { describe, expect, it } from "vitest";
import { forbiddenWordsModule } from "./forbidden-words";
import { bingoModule } from "./bingo";
import { fortunatelyModule } from "./fortunately";
import { oneWordStoryModule } from "./one-word-story";
import { randomPickerModule } from "./random-picker";
import { spinWheelModule } from "./spin-wheel";
import { triviaModule } from "./trivia";
import { sketchGuessModule } from "./sketch-guess";
import { wordSpiesModule } from "./word-spies";
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

  describe("duplicate entries that differ only by accent or punctuation (the server folds these)", () => {
    const CAFE = "Caf\u00e9";

    it("Spin the Wheel names the problem and blocks Next", () => {
      const setup = { ...spinWheelModule.defaultSetup, segments: [CAFE, "cafe"] };
      expect(spinWheelModule.setupIssue?.(setup)).toBe("Each wheel segment must be different.");
      expect(spinWheelModule.isSetupValid(setup)).toBe(false);
    });

    it("Bingo names the problem and blocks Next", () => {
      const setup = { ...bingoModule.defaultSetup, items: [CAFE, "cafe!"] };
      expect(bingoModule.setupIssue?.(setup)).toBe("Each bingo item must be different.");
      expect(bingoModule.isSetupValid(setup)).toBe(false);
    });

    it("Word Spies names the problem and blocks Next", () => {
      const setup = { ...wordSpiesModule.defaultSetup, words: [CAFE, "cafe"] };
      expect(wordSpiesModule.setupIssue?.(setup)).toBe("Each word must be different.");
      expect(wordSpiesModule.isSetupValid(setup)).toBe(false);
    });

    it("all three stay silent for a ready setup", () => {
      expect(spinWheelModule.setupIssue?.(spinWheelModule.defaultSetup)).toBeNull();
      expect(bingoModule.setupIssue?.(bingoModule.defaultSetup)).toBeNull();
      expect(wordSpiesModule.setupIssue?.(wordSpiesModule.defaultSetup)).toBeNull();
    });

    it("Spin the Wheel asks for more segments when the built-ins are off", () => {
      expect(spinWheelModule.setupIssue?.({ ...spinWheelModule.defaultSetup, useBuiltIn: false, segments: ["One"] })).toMatch(/at least 2 segments/);
    });
  });

  describe("Trivia limits", () => {
    const { defaultSetup, setupIssue, isSetupValid } = triviaModule;
    const q = (text: string, options = ["Yes", "No"]) => ({ text, options, correctIndex: 0, category: "" });
    const many = (n: number) => Array.from({ length: n }, (_, i) => q(`Question ${i + 1}?`));

    it("is silent for a ready setup", () => {
      expect(setupIssue?.({ ...defaultSetup, questions: [q("One?")] })).toBeNull();
    });

    it("names the first unfinished question", () => {
      expect(setupIssue?.({ ...defaultSetup, questions: [q("One?"), q("Two?", ["Only one", ""])] })).toBe(
        "Question 2 needs a question, at least two options and a correct answer.",
      );
    });

    it("refuses more than 6 options or an option over 100 characters", () => {
      const seven = Array.from({ length: 7 }, (_, i) => `Option ${i}`);
      expect(isSetupValid({ ...defaultSetup, questions: [q("Many?", seven)] })).toBe(false);
      expect(isSetupValid({ ...defaultSetup, questions: [q("Long?", ["x".repeat(101), "No"])] })).toBe(false);
      expect(isSetupValid({ ...defaultSetup, questions: [q("Max?", Array.from({ length: 6 }, () => "x".repeat(100)))] })).toBe(true);
    });

    it("counts the starter questions towards the limit of 50", () => {
      expect(setupIssue?.({ ...defaultSetup, questions: many(50) })).toBeNull();
      expect(setupIssue?.({ ...defaultSetup, questions: many(51) })).toMatch(/at most 50/);
      expect(setupIssue?.({ ...defaultSetup, questions: many(40), useBuiltIn: true, builtInCount: 11 })).toMatch(/starter/);
    });

    it("asks for something to play when there are no questions and no starter set", () => {
      expect(setupIssue?.({ ...defaultSetup, questions: [q("", ["", ""])], useBuiltIn: false })).toBe("Add at least one question, or turn on the starter set.");
    });
  });
});
