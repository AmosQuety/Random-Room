import type { AnyGameModule, GameCategory } from "./types";

export interface CategoryInfo {
  key: GameCategory;
  label: string;
  blurb: string;
}

/** Display order for the picker. A category with no registered games is simply not shown. */
export const CATEGORIES: readonly CategoryInfo[] = [
  { key: "poll", label: "Poll & Reveal", blurb: "Everyone answers privately, then it all reveals at once." },
  { key: "quiz", label: "Quiz", blurb: "Questions, answers and a scoreboard." },
  { key: "word", label: "Word & Drawing", blurb: "Say it, spell it, sketch it." },
  { key: "reflex", label: "Reflex & Chance", blurb: "Fast fingers and lucky draws." },
  { key: "story", label: "Story", blurb: "Build something ridiculous together." },
];

export interface GameGroup {
  category: CategoryInfo;
  games: AnyGameModule[];
}

/** Groups games by category in display order, dropping empty categories. */
export function groupByCategory(games: readonly AnyGameModule[]): GameGroup[] {
  return CATEGORIES.map((category) => ({ category, games: games.filter((g) => g.category === category.key) })).filter(
    (group) => group.games.length > 0,
  );
}
