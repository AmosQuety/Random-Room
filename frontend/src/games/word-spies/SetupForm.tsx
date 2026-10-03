import { ListEditor } from "../../components/ListEditor";
import type { SetupFormProps } from "../types";
import { filledWords } from "./setup";
import { BOARD_SIZE, BUILT_IN_WORDS, MAX_WORD_LENGTH, MAX_WORDS, type SpySetup } from "./types";

export function WordSpiesSetupForm({ value, onChange }: SetupFormProps<SpySetup>) {
  const update = (patch: Partial<SpySetup>) => onChange({ ...value, ...patch });
  const filled = filledWords(value.words);
  const duplicated = new Set(filled.map((w) => w.toLowerCase())).size !== filled.length;
  const pool = filled.length + (value.useBuiltIn ? BUILT_IN_WORDS : 0);

  return (
    <div className="flex flex-col gap-6">
      <p className="rounded-lg border-2 border-ink bg-card p-4 text-sm text-muted">
        Players are split at random into a red and a blue team, each with one spymaster. Only the spymasters see which words
        belong to which team. They give one-word clues, and their team guesses. Find all your words first, and avoid the assassin.
        Needs 4 or more players.
      </p>

      <label className="flex min-h-11 cursor-pointer items-start gap-3 rounded-lg border-2 border-ink bg-card p-4">
        <input
          type="checkbox"
          checked={value.useBuiltIn}
          onChange={(e) => update({ useBuiltIn: e.target.checked })}
          className="mt-1 size-5 shrink-0 accent-[var(--accent)]"
        />
        <span>
          <span className="block font-bold">Use the built-in words</span>
          <span className="block text-sm text-muted">{BUILT_IN_WORDS} everyday nouns. Each game deals a fresh 25.</span>
        </span>
      </label>

      <ListEditor
        legend="Your own words"
        hint={`Up to ${MAX_WORDS}. Add them to the pool to make the board yours.`}
        items={value.words}
        onChange={(words) => update({ words })}
        placeholder={(i) => `Word ${i + 1}`}
        minItems={0}
        maxItems={MAX_WORDS}
        maxLength={MAX_WORD_LENGTH}
        addLabel="Add a word"
      />

      <p role="status" className={`text-sm ${duplicated || pool < BOARD_SIZE ? "font-semibold text-tomato" : "text-muted"}`}>
        {duplicated ? "Each word needs to be different." : pool < BOARD_SIZE ? `The board needs at least ${BOARD_SIZE} words. You have ${pool}.` : `${pool} words in the pool.`}
      </p>
    </div>
  );
}
