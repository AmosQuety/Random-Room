import type { BuzzerSetup } from "./types";

export const filledPrompts = (prompts: string[]): string[] => prompts.map((p) => p.trim()).filter(Boolean);

export const isBuzzerSetupValid = (setup: BuzzerSetup): boolean => setup.useBuiltIn || filledPrompts(setup.prompts).length > 0;

export const toApiBuzzerSetup = (setup: BuzzerSetup) => ({
  prompts: filledPrompts(setup.prompts),
  useBuiltIn: setup.useBuiltIn,
  rounds: setup.rounds,
});
