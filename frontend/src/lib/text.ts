/**
 * Makes free text comparable the way the server does (AnswerNormalizer): case, accents, punctuation and spacing
 * do not matter. The form uses it to spot duplicates the server would refuse.
 */
export function normalizeAnswer(text: string): string {
  return text
    .normalize("NFD")
    .replace(/\p{M}/gu, "")
    .toLowerCase()
    .replace(/[^\p{L}\p{N}]+/gu, " ")
    .trim();
}
