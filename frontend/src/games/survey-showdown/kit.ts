import type { PromptKit } from "../rounds/setup";

export interface SurveyRow {
  text: string;
  points: string;
}

export interface SurveyInput {
  text: string;
  answers: SurveyRow[];
}

/** Matches backend Games/Content/survey-showdown.json. */
export const BUILT_IN_COUNT = 15;
export const MAX_BOARD = 6;

const emptyRow = (): SurveyRow => ({ text: "", points: "" });
const filledRows = (p: SurveyInput) => p.answers.filter((a) => a.text.trim() !== "" || a.points.trim() !== "");
const pointsOf = (row: SurveyRow) => Number(row.points);
const rowValid = (row: SurveyRow) => row.text.trim() !== "" && Number.isInteger(pointsOf(row)) && pointsOf(row) >= 1 && pointsOf(row) <= 100;

export const kit: PromptKit<SurveyInput> = {
  builtInCount: BUILT_IN_COUNT,
  empty: () => ({ text: "", answers: [emptyRow(), emptyRow(), emptyRow()] }),
  isFilled: (p) => p.text.trim() !== "" || filledRows(p).length > 0,
  isValid: (p) => p.text.trim() !== "" && filledRows(p).length >= 2 && filledRows(p).every(rowValid),
  toApi: (p) => ({
    text: p.text.trim(),
    answers: filledRows(p).map((a) => ({ text: a.text.trim(), points: pointsOf(a) })),
  }),
};

export { emptyRow };
