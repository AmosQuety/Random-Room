import type { PromptKit } from "./setup";

/** One line of text per prompt: Most Likely To and Never Have I Ever. */
export interface StatementInput {
  text: string;
}

export interface StatementPromptView {
  text: string;
}

export function statementKit(builtInCount: number): PromptKit<StatementInput> {
  return {
    builtInCount,
    empty: () => ({ text: "" }),
    isFilled: (p) => p.text.trim() !== "",
    isValid: (p) => p.text.trim() !== "" && p.text.trim().length <= 140,
    problem: () => "Write the statement you started (at most 140 characters), or clear it.",
    toApi: (p) => ({ text: p.text.trim() }),
  };
}
