/** Four toy blocks in a tile: circle, square, triangle, arch. Pure SVG so it costs no image bytes. */
export function LogoMark({ className = "size-9" }: { className?: string }) {
  return (
    <svg viewBox="0 0 48 48" className={className} aria-hidden="true">
      <rect x="3" y="3" width="42" height="42" rx="11" className="fill-card stroke-ink" strokeWidth="3" />
      <circle cx="16.5" cy="16.5" r="7" className="fill-tomato stroke-ink" strokeWidth="2" />
      <rect x="26" y="9.5" width="13" height="13" rx="2.5" className="fill-sky stroke-ink" strokeWidth="2" />
      <path d="M9.5 38.5L16.5 26l7 12.5z" className="fill-mustard stroke-ink" strokeWidth="2" strokeLinejoin="round" />
      <path d="M26 38.5a6.5 6.5 0 0113 0z" className="fill-leaf stroke-ink" strokeWidth="2" strokeLinejoin="round" />
    </svg>
  );
}

/** The compact brand lockup used in the persistent header. */
export function Wordmark() {
  return (
    <span className="flex items-center gap-2.5">
      <LogoMark />
      <span className="font-display text-[1.35rem] font-black leading-none tracking-tight">
        <span className="mr-1.5 font-mono text-[0.6rem] font-bold uppercase tracking-[0.25em] align-middle text-muted">The</span>
        Playground
      </span>
    </span>
  );
}

/** The home-screen hero wordmark: the brand is the loudest thing on the page. */
export function HeroWordmark() {
  return (
    <h1 className="flex flex-col items-start gap-3">
      <span className="flex items-center gap-4">
        <LogoMark className="size-16 rotate-[-6deg] motion-safe:animate-wobble sm:size-24" />
        <span className="rotate-[-3deg] rounded-md border-2 border-ink bg-mustard px-3 py-1 font-mono text-sm font-bold uppercase tracking-[0.3em] text-ink shadow-ticket-sm sm:text-base">
          The
        </span>
      </span>
      <span className="block font-display text-[clamp(2.6rem,14.2vw,9rem)] font-black leading-[0.85] tracking-[-0.03em]">
        Playground
      </span>
      <svg viewBox="0 0 320 16" className="-mt-1 h-3 w-[min(80%,26rem)] text-tomato" aria-hidden="true" preserveAspectRatio="none">
        <path
          d="M2 9c20-10 30 8 50 0s30 8 50 0 30 8 50 0 30 8 50 0 30 8 50 0 30 8 66 0"
          fill="none"
          stroke="currentColor"
          strokeWidth="5"
          strokeLinecap="round"
        />
      </svg>
    </h1>
  );
}
