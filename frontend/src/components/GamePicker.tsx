import { useId, useState } from "react";
import { groupByCategory } from "../games/categories";
import type { AnyGameModule } from "../games/types";
import { describePlayerRange } from "../lib/players";
import { CheckIcon, SearchIcon } from "./icons";
import { EmptyState } from "./ui";
import { inputClass, sectionHeadingClass } from "./styles";

/** Above this many games a search box earns its place; below it, scanning the grid is faster. */
const SEARCH_THRESHOLD = 8;

interface Props {
  games: readonly AnyGameModule[];
  selectedKey: string | null;
  onSelect: (key: string) => void;
}

function matches(game: AnyGameModule, query: string): boolean {
  const needle = query.trim().toLowerCase();
  if (!needle) return true;
  return [game.name, game.hook, game.description].some((text) => text.toLowerCase().includes(needle));
}

function GameTile({ game, selected, onSelect }: { game: AnyGameModule; selected: boolean; onSelect: () => void }) {
  return (
    <label
      data-accent={game.accent}
      className={`group relative flex h-full cursor-pointer flex-col gap-2 rounded-xl border-2 border-ink p-3 transition has-[:focus-visible]:outline-3 has-[:focus-visible]:outline-offset-3 has-[:focus-visible]:outline-sky sm:p-4 ${
        selected
          ? "bg-accent text-on-accent shadow-ticket"
          : "bg-accent-soft text-ink shadow-ticket-sm hover:-translate-y-0.5 hover:shadow-ticket"
      }`}
    >
      <input type="radio" name="game" value={game.key} checked={selected} onChange={onSelect} className="sr-only" />
      <span className="flex items-start justify-between gap-2">
        <span className="grid size-12 place-items-center rounded-lg border-2 border-ink bg-card">
          <game.Glyph className="size-9" />
        </span>
        {selected && (
          <span className="grid size-7 animate-pop place-items-center rounded-full border-2 border-ink bg-card text-ink">
            <CheckIcon className="size-4" />
            <span className="sr-only">Selected</span>
          </span>
        )}
      </span>
      <span className="font-display text-lg font-black leading-tight">{game.name}</span>
      <span className="text-sm leading-snug">{game.hook}</span>
      <span className="mt-auto pt-1 font-mono text-xs font-bold uppercase tracking-wider">{describePlayerRange(game)}</span>
    </label>
  );
}

/** Games grouped by category as a grid of radio tiles. Native radios give keyboard and screen-reader behaviour for free. */
export function GamePicker({ games, selectedKey, onSelect }: Props) {
  const [query, setQuery] = useState("");
  const searchId = useId();
  const visible = games.filter((game) => matches(game, query));
  const groups = groupByCategory(visible);

  return (
    <div className="flex flex-col gap-6">
      {games.length > SEARCH_THRESHOLD && (
        <div>
          <label htmlFor={searchId} className={`mb-2 block ${sectionHeadingClass}`}>
            Search games
          </label>
          <div className="relative">
            <SearchIcon className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-muted" />
            <input
              id={searchId}
              type="search"
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              placeholder="Try &quot;story&quot; or &quot;quiz&quot;"
              className={`${inputClass} pl-11`}
            />
          </div>
          <p role="status" className="sr-only">
            {visible.length} {visible.length === 1 ? "game" : "games"} shown
          </p>
        </div>
      )}

      {groups.length === 0 && (
        <EmptyState title="No games match that">
          <p>Try a different word, or clear the search to see everything.</p>
        </EmptyState>
      )}

      {/* Every tile shares one radio name, so arrow keys move through all categories. */}
      <fieldset className="flex min-w-0 flex-col gap-8">
        <legend className="sr-only">Choose a game</legend>
        {groups.map(({ category, games: inCategory }) => (
          <section key={category.key} aria-labelledby={`cat-${category.key}`}>
            <div className="mb-3">
              <h4 id={`cat-${category.key}`} className="font-display text-xl font-black">
                {category.label}
                <span className="ml-2 font-mono text-xs font-normal text-muted">{inCategory.length}</span>
              </h4>
              <p className="text-sm text-muted">{category.blurb}</p>
            </div>
            <div className="grid grid-cols-2 gap-3 md:grid-cols-3 lg:grid-cols-4">
              {inCategory.map((game) => (
                <GameTile key={game.key} game={game} selected={selectedKey === game.key} onSelect={() => onSelect(game.key)} />
              ))}
            </div>
          </section>
        ))}
      </fieldset>
    </div>
  );
}
