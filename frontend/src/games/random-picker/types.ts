export interface RandomPickerPlayerState {
  hasTriggered: boolean;
  result: string | null;
}

export interface ActivityView {
  eventId: string;
  sessionId: string;
  sessionNumber: number;
  triggeredBy: string;
  result: string;
  timestamp: string;
}

export interface RandomPickerPayload {
  choices: string[];
  players: Record<string, RandomPickerPlayerState>;
  tally: { choice: string; count: number }[];
  activity: ActivityView[];
}

export interface RandomPickerPreview {
  choices: string[];
}

/** Local setup-form state: a plain list of choice labels, at least 2 non-empty. */
export type RandomPickerSetup = string[];
