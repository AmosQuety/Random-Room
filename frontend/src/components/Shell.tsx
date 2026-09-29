import type { ReactNode } from "react";
import type { Accent } from "../games/types";
import { Wordmark } from "./Logo";
import { AppLink } from "./ui";

const WIDTH = {
  narrow: "max-w-md",
  medium: "max-w-2xl",
  wide: "max-w-5xl",
} as const;

interface Props {
  children: ReactNode;
  width?: keyof typeof WIDTH;
  /** Content for the right side of the header, such as connection status or the current player. */
  headerRight?: ReactNode;
  accent?: Accent;
}

/** The frame every screen shares: skip link, persistent wordmark header, and a single <main> landmark. */
export function Shell({ children, width = "narrow", headerRight, accent }: Props) {
  return (
    <div data-accent={accent} className="flex min-h-dvh flex-col">
      <a
        href="#main"
        className="sr-only-focusable fixed left-3 top-3 z-50 rounded-lg border-2 border-ink bg-card px-4 py-2 font-bold"
      >
        Skip to content
      </a>
      <header className="border-b-2 border-ink bg-paper">
        <div className="mx-auto flex min-h-16 max-w-5xl items-center justify-between gap-3 px-4">
          <AppLink href="/" aria-label="The Playground home" className="-mx-1 rounded-lg px-1 py-2">
            <Wordmark />
          </AppLink>
          {headerRight && <div className="flex min-w-0 items-center gap-3 text-sm">{headerRight}</div>}
        </div>
      </header>
      <main id="main" tabIndex={-1} className={`mx-auto w-full flex-1 px-4 py-8 outline-none sm:py-12 ${WIDTH[width]}`}>
        {children}
      </main>
      <footer className="border-t-2 border-dashed border-ink/40 px-4 py-5 text-center font-mono text-xs uppercase tracking-widest text-muted">
        Every result is decided on the server. Nobody can cheat from their browser.
      </footer>
    </div>
  );
}
