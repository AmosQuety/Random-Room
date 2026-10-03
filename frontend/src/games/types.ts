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

/** Picker groups, in display order. */
export type GameCategory = "poll" | "quiz" | "word" | "reflex" | "story";

/** Each accent maps to a token set in index.css (data-accent), so a game re-themes its whole room. */
export type Accent = "tomato" | "sky" | "leaf" | "mustard" | "plum";

export interface GlyphProps {
  className?: string;
}

/** One pluggable game type, mirroring the backend's IGameEngine: setup shape, gameplay UI, and payload type. */
export interface GameModule<TSetup, TPayload> {
  key: string;
  name: string;
  /** Longer explanation, shown where there is room (join screen, setup header). */
  description: string;
  /** One line that sells the game in a picker tile. */
  hook: string;
  category: GameCategory;
  accent: Accent;
  /** A small illustrative SVG, drawn with token classes so it follows the game's accent. */
  Glyph: ComponentType<GlyphProps>;
  minPlayers: number;
  maxPlayers: number;
  defaultSetup: TSetup;
  isSetupValid: (setup: TSetup) => boolean;
  /** Why the setup is not ready, in words for the host, or null when it is. Shown in place of a generic message. */
  setupIssue?: (setup: TSetup) => string | null;
  /** Converts the form's local state into the JSON body the room-creation setup expects. */
  toApiSetup: (setup: TSetup) => unknown;
  /** These two may be React.lazy components so each game ships as its own chunk. */
  SetupForm: ComponentType<SetupFormProps<TSetup>>;
  GameScreen: ComponentType<GameScreenProps<TPayload>>;
}

/** The registry is heterogeneous: each module's own TSetup/TPayload are only known inside its folder. */
// eslint-disable-next-line @typescript-eslint/no-explicit-any
export type AnyGameModule = GameModule<any, any>;
