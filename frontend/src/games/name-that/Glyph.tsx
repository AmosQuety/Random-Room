import type { GlyphProps } from "../types";

export function NameThatGlyph({ className = "size-10" }: GlyphProps) {
  return (
    <svg viewBox="0 0 48 48" className={className} aria-hidden="true">
      <path d="M17 34V11l20-4v23" className="stroke-ink" fill="none" strokeWidth="3.5" strokeLinejoin="round" />
      <ellipse cx="12" cy="35" rx="7" ry="5.5" className="fill-accent stroke-ink" strokeWidth="3" />
      <ellipse cx="32" cy="31" rx="7" ry="5.5" className="fill-accent stroke-ink" strokeWidth="3" />
    </svg>
  );
}
