import { describe, expect, it } from "vitest";
import { availablePromptCount, effectiveRounds, roundOptions, roundSetupFor } from "./setup";
import { twoWayKit } from "./twoWay";

const kit = twoWayKit(30);
const helpers = roundSetupFor(kit);

describe("round setup", () => {
  it("starts with the built-in prompts on and nothing custom", () => {
    expect(helpers.defaultSetup).toMatchObject({ useBuiltIn: true, custom: [], timeLimit: null });
    expect(helpers.isSetupValid(helpers.defaultSetup)).toBe(true);
  });

  it("with no built-in prompts, needs at least one complete custom prompt", () => {
    const own = roundSetupFor(twoWayKit(0));
    expect(own.isSetupValid(own.defaultSetup)).toBe(false);
    expect(own.isSetupValid({ ...own.defaultSetup, custom: [{ a: "Cats", b: "Dogs" }] })).toBe(true);
  });

  it("turning built-ins off with nothing custom is not valid", () => {
    expect(helpers.isSetupValid({ ...helpers.defaultSetup, useBuiltIn: false, custom: [{ a: "", b: "" }] })).toBe(false);
  });

  it("ignores untouched rows but rejects a half-written one", () => {
    const base = { ...helpers.defaultSetup };
    expect(helpers.isSetupValid({ ...base, custom: [{ a: "", b: "" }] })).toBe(true);
    expect(helpers.isSetupValid({ ...base, custom: [{ a: "Cats", b: "" }] })).toBe(false);
  });

  it("counts built-in plus filled custom prompts", () => {
    expect(availablePromptCount({ ...helpers.defaultSetup, custom: [{ a: "x", b: "y" }, { a: "", b: "" }] }, kit)).toBe(31);
  });

  it("never asks for more rounds than there are prompts", () => {
    const setup = { ...helpers.defaultSetup, useBuiltIn: false, custom: [{ a: "1", b: "2" }, { a: "3", b: "4" }], rounds: 8 };
    expect(effectiveRounds(setup, kit)).toBe(2);
    expect(helpers.toApiSetup(setup)).toMatchObject({ rounds: 2, useBuiltIn: false });
  });

  it("builds the API body with trimmed prompts and an optional time limit", () => {
    const body = helpers.toApiSetup({ useBuiltIn: true, custom: [{ a: " Cats ", b: " Dogs " }], rounds: 5, timeLimit: 30 });
    expect(body).toEqual({ useBuiltIn: true, prompts: [{ a: "Cats", b: "Dogs" }], rounds: 5, timeLimitSeconds: 30 });
  });

  it("omits the time limit when there is none", () => {
    expect(helpers.toApiSetup(helpers.defaultSetup)).not.toHaveProperty("timeLimitSeconds");
  });

  it("offers the usual round counts that fit, plus all of them", () => {
    expect(roundOptions(12, 8)).toEqual([3, 5, 8, 10, 12]);
    expect(roundOptions(4, 4)).toEqual([3, 4]);
    expect(roundOptions(1, 1)).toEqual([1]);
  });
});
