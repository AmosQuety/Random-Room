export interface ScoreRow {
  name: string;
  score: number;
}

/** Competition ranking: equal scores share a rank, and the next distinct score skips ahead (1, 1, 3). */
export function rankRows(rows: ScoreRow[]): { row: ScoreRow; rank: number }[] {
  const sorted = [...rows].sort((a, b) => b.score - a.score);
  const ranked: { row: ScoreRow; rank: number }[] = [];
  sorted.forEach((row, i) => {
    const tied = i > 0 && sorted[i - 1].score === row.score;
    ranked.push({ row, rank: tied ? ranked[i - 1].rank : i + 1 });
  });
  return ranked;
}
