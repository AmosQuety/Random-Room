import type { GlyphProps } from "../types";

export function RandomPickerGlyph({ className = "size-10" }: GlyphProps) {
  return (
    <svg viewBox="0 0 48 48" className={className} aria-hidden="true">
      <g transform="rotate(-8 24 24)">
        <rect x="7" y="7" width="34" height="34" rx="9" className="fill-accent stroke-ink" strokeWidth="3" />
        <circle cx="17" cy="17" r="3" className="fill-on-accent" />
        <circle cx="31" cy="17" r="3" className="fill-on-accent" />
        <circle cx="24" cy="24" r="3" className="fill-on-accent" />
        <circle cx="17" cy="31" r="3" className="fill-on-accent" />
        <circle cx="31" cy="31" r="3" className="fill-on-accent" />
      </g>
    </svg>
  );
}
