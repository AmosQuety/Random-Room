import type { GlyphProps } from "../types";

export function TriviaGlyph({ className = "size-10" }: GlyphProps) {
  return (
    <svg viewBox="0 0 48 48" className={className} aria-hidden="true">
      <path
        d="M9 8h30a4 4 0 014 4v20a4 4 0 01-4 4H24l-9 7v-7H9a4 4 0 01-4-4V12a4 4 0 014-4z"
        className="fill-accent stroke-ink"
        strokeWidth="3"
        strokeLinejoin="round"
      />
      <path
        d="M19 17.5c0-3 2.3-5 5.2-5s5.3 1.9 5.3 4.6c0 3.4-4.5 3.6-4.5 7"
        className="stroke-on-accent"
        fill="none"
        strokeWidth="3.4"
        strokeLinecap="round"
      />
      <circle cx="25" cy="29.5" r="2.1" className="fill-on-accent" />
    </svg>
  );
}
