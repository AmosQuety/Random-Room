/** Shared class strings. Kept out of component files so fast refresh keeps working. */

export type ButtonVariant = "primary" | "accent" | "secondary" | "dark";
export type ButtonSize = "sm" | "md" | "lg";

const VARIANT: Record<ButtonVariant, string> = {
  primary: "bg-tomato text-white shadow-ticket active:translate-x-1 active:translate-y-1",
  accent: "bg-accent text-on-accent shadow-ticket active:translate-x-1 active:translate-y-1",
  secondary: "bg-card text-ink shadow-ticket-sm active:translate-x-0.5 active:translate-y-0.5",
  dark: "surface-dark bg-ink text-paper shadow-ticket-sm active:translate-x-0.5 active:translate-y-0.5",
};

const SIZE: Record<ButtonSize, string> = {
  sm: "min-h-11 px-4 text-sm",
  md: "min-h-12 px-5 text-base",
  lg: "min-h-14 px-6 text-lg",
};

/** Ticket-style button classes, also usable on anchors that should look like buttons. */
export function buttonClass(variant: ButtonVariant = "primary", size: ButtonSize = "md"): string {
  return `inline-flex items-center justify-center gap-2 rounded-lg border-2 border-ink font-black uppercase tracking-wide transition active:shadow-none disabled:cursor-not-allowed disabled:bg-muted disabled:text-white disabled:shadow-none ${VARIANT[variant]} ${SIZE[size]}`;
}

export const sectionHeadingClass = "font-mono text-sm font-bold uppercase tracking-widest";

export const inputClass =
  "min-h-12 w-full rounded-lg border-2 border-ink bg-card px-3 text-lg placeholder:text-muted/80 aria-[invalid=true]:border-tomato";

export const fieldInputClass = inputClass;
export const removeButtonClass =
  "grid size-11 shrink-0 place-items-center rounded-lg border-2 border-ink bg-card text-muted transition hover:bg-paper-deep";
export const addButtonClass =
  "inline-flex min-h-12 items-center justify-center gap-2 rounded-lg border-2 border-dashed border-ink px-4 font-mono text-sm font-bold uppercase tracking-widest text-muted transition hover:bg-card disabled:cursor-not-allowed disabled:opacity-60";
