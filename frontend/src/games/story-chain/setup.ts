import type { ChainConfig, ChainSetup } from "./types";
import { BUILT_IN_OPENERS } from "./types";

export const filledOpeners = (openers: string[]): string[] => openers.map((o) => o.trim()).filter(Boolean);

export const isChainSetupValid = (setup: ChainSetup): boolean => setup.useBuiltIn || filledOpeners(setup.openers).length > 0;

export const chainSetupIssue = (setup: ChainSetup): string | null =>
  isChainSetupValid(setup) ? null : "Add at least one story starter, or turn on the built-in ones.";

export const toApiChainSetup = (setup: ChainSetup) => ({
  openers: filledOpeners(setup.openers),
  useBuiltIn: setup.useBuiltIn,
  length: setup.length,
});

export const defaultChainSetup = (config: ChainConfig): ChainSetup => ({ openers: [""], useBuiltIn: BUILT_IN_OPENERS > 0, length: config.defaultLength });
