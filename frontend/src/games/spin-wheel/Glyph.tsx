import type { GlyphProps } from "../types";

export function SpinWheelGlyph({ className = "size-10" }: GlyphProps) {
  return (
    <svg viewBox="0 0 48 48" className={className} aria-hidden="true">
      <circle cx="24" cy="26" r="17" className="fill-card stroke-ink" strokeWidth="3" />
      <path d="M24 26V9a17 17 0 0114.7 8.5z" className="fill-accent stroke-ink" strokeWidth="2" strokeLinejoin="round" />
      <path d="M24 26l-14.7 8.5A17 17 0 0124 43z" className="fill-accent stroke-ink" strokeWidth="2" strokeLinejoin="round" />
      <circle cx="24" cy="26" r="3" className="fill-ink" />
      <path d="M24 3l4 7h-8z" className="fill-tomato stroke-ink" strokeWidth="2" strokeLinejoin="round" />
    </svg>
  );
}
