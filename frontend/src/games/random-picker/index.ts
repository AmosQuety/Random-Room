import type { GameModule } from "../types";
import { RandomPickerGameScreen } from "./GameScreen";
import { RandomPickerSetupForm } from "./SetupForm";
import type { RandomPickerPayload, RandomPickerSetup } from "./types";

export const randomPickerModule: GameModule<RandomPickerSetup, RandomPickerPayload> = {
  key: "random-picker",
  name: "Random Picker",
  description: "Everyone triggers a server-random pick from a shared list. Nobody chooses for themselves.",
  defaultSetup: ["", ""],
  isSetupValid: (setup) => setup.map((c) => c.trim()).filter(Boolean).length >= 2,
  toApiSetup: (setup) => ({ choices: setup.map((c) => c.trim()).filter(Boolean) }),
  SetupForm: RandomPickerSetupForm,
  GameScreen: RandomPickerGameScreen,
};
