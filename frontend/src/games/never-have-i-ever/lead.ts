const LEAD_IN = /^never have i ever[\s.,:;-]*/i;

/**
 * The screen already shows "Never have I ever..." above the statement, and the built-in statements (and hosts' own)
 * often include it, so it would read twice. Drop it from the statement when drawing it.
 */
export function withoutLead(text: string): string {
  const rest = text.replace(LEAD_IN, "").trim();
  return rest === "" ? text : rest;
}
