import type { ComponentType } from "react";
import type { PlayerView, SessionView } from "../lib/types";

export interface GameScreenProps<TPayload> {
  me: string;
  isHost: boolean;
  players: PlayerView[];
  session: SessionView;
  payload: TPayload;
  busy: boolean;
  onAction: (action: string, payload?: unknown) => void;
}

export interface SetupFormProps<TSetup> {
  value: TSetup;
  onChange: (value: TSetup) => void;
}

/** One pluggable game type, mirroring the backend's IGameEngine: setup shape, gameplay UI, and payload type. */
export interface GameModule<TSetup, TPayload> {
  key: string;
  name: string;
  description: string;
  defaultSetup: TSetup;
  isSetupValid: (setup: TSetup) => boolean;
  /** Converts the form's local state into the JSON body the room-creation setup expects. */
  toApiSetup: (setup: TSetup) => unknown;
  SetupForm: ComponentType<SetupFormProps<TSetup>>;
  GameScreen: ComponentType<GameScreenProps<TPayload>>;
}
