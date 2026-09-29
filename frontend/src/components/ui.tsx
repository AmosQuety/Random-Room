import type { AnchorHTMLAttributes, ButtonHTMLAttributes, HTMLAttributes, ReactNode } from "react";
import { navigate } from "../lib/router";
import { DiceIcon, WarningIcon } from "./icons";
import { buttonClass, sectionHeadingClass, type ButtonSize, type ButtonVariant } from "./styles";

/* ---------- Buttons ---------- */

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant;
  size?: ButtonSize;
}

export function Button({ variant, size, className = "", type = "button", ...rest }: ButtonProps) {
  return <button type={type} className={`${buttonClass(variant, size)} ${className}`} {...rest} />;
}

/** In-app link: real anchor for semantics and open-in-new-tab, but navigates client-side on a plain click. */
export function AppLink({ href, onClick, ...rest }: AnchorHTMLAttributes<HTMLAnchorElement> & { href: string }) {
  return (
    <a
      href={href}
      onClick={(event) => {
        onClick?.(event);
        const plain = event.button === 0 && !event.metaKey && !event.ctrlKey && !event.shiftKey && !event.altKey;
        if (!event.defaultPrevented && plain) {
          event.preventDefault();
          navigate(href);
        }
      }}
      {...rest}
    />
  );
}

/* ---------- Surfaces and text ---------- */

export function Card({ className = "", ...rest }: HTMLAttributes<HTMLDivElement>) {
  return <div className={`rounded-xl border-2 border-ink bg-card p-5 shadow-ticket sm:p-6 ${className}`} {...rest} />;
}

export function Eyebrow({ className = "", ...rest }: HTMLAttributes<HTMLParagraphElement>) {
  return <p className={`font-mono text-xs uppercase tracking-widest text-muted ${className}`} {...rest} />;
}

/* ---------- Form fields ---------- */

interface FieldProps {
  id: string;
  label: string;
  hint?: string;
  error?: string | null;
  children: ReactNode;
}

/** Label + hint + error wrapper. The control inside should set aria-describedby={`${id}-hint`}. */
export function Field({ id, label, hint, error, children }: FieldProps) {
  return (
    <div>
      <label htmlFor={id} className={`mb-2 block ${sectionHeadingClass}`}>
        {label}
      </label>
      {hint && (
        <p id={`${id}-hint`} className="-mt-1 mb-2 text-sm text-muted">
          {hint}
        </p>
      )}
      {children}
      {error && (
        <p role="alert" className="mt-2 text-sm font-semibold text-tomato">
          {error}
        </p>
      )}
    </div>
  );
}

/* ---------- Feedback states ---------- */

export function Alert({ children, title }: { children: ReactNode; title?: string }) {
  return (
    <div role="alert" className="flex items-start gap-3 rounded-lg border-2 border-tomato bg-card px-4 py-3 text-tomato">
      <WarningIcon className="mt-0.5 size-5 shrink-0" />
      <div>
        {title && <p className="font-black">{title}</p>}
        <p className="font-semibold">{children}</p>
      </div>
    </div>
  );
}

export function Skeleton({ className = "" }: { className?: string }) {
  return <div aria-hidden="true" className={`animate-shimmer rounded-lg border-2 border-ink/15 bg-paper-deep ${className}`} />;
}

/** Full-page loading state: a rolling die and a line of copy, announced politely to screen readers. */
export function LoadingPanel({ label }: { label: string }) {
  return (
    <div role="status" className="flex flex-col items-center gap-4 py-16 text-center">
      <DiceIcon className="size-12 text-tomato motion-safe:animate-roll" />
      <p className="font-mono text-sm uppercase tracking-widest text-muted">{label}</p>
    </div>
  );
}

interface EmptyStateProps {
  title: string;
  children?: ReactNode;
  action?: ReactNode;
  tone?: "neutral" | "error";
}

export function EmptyState({ title, children, action, tone = "neutral" }: EmptyStateProps) {
  return (
    <div
      role={tone === "error" ? "alert" : undefined}
      className="flex flex-col items-center gap-3 rounded-xl border-2 border-dashed border-ink px-6 py-10 text-center"
    >
      <h2 className={`font-display text-2xl font-black ${tone === "error" ? "text-tomato" : ""}`}>{title}</h2>
      {children && <div className="max-w-sm text-muted">{children}</div>}
      {action && <div className="mt-2 flex flex-wrap justify-center gap-3">{action}</div>}
    </div>
  );
}

/** Online/offline is conveyed by both the dot and the word, never colour alone. */
export function PresenceDot({ online }: { online: boolean }) {
  return (
    <span className="inline-flex items-center gap-1.5 text-sm text-muted">
      <span
        aria-hidden="true"
        className={`size-2.5 rounded-full border-2 border-ink ${online ? "bg-leaf" : "bg-transparent"}`}
      />
      {online ? "Online" : "Offline"}
    </span>
  );
}
