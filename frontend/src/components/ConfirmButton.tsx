import { useEffect, useRef, useState, type KeyboardEvent, type ReactNode } from "react";
import { Button } from "./ui";
import type { ButtonVariant } from "./styles";

interface Props {
  /** The trigger button's contents. */
  children: ReactNode;
  /** Shown once the trigger is pressed, e.g. "End the game for everyone?". */
  question: string;
  confirmLabel: string;
  cancelLabel: string;
  onConfirm: () => void;
  disabled?: boolean;
  variant?: ButtonVariant;
}

/**
 * A button for an action that cannot be undone. The first press only asks; the action runs on the second.
 * It confirms inline rather than in a modal, so there is no focus trap and it stays light on slow phones.
 * Focus moves to the safe choice, and Escape or cancelling returns it to the original button.
 */
export function ConfirmButton({ children, question, confirmLabel, cancelLabel, onConfirm, disabled, variant = "secondary" }: Props) {
  const [asking, setAsking] = useState(false);
  const triggerRef = useRef<HTMLButtonElement>(null);
  const cancelRef = useRef<HTMLButtonElement>(null);
  const wasAsking = useRef(false);

  useEffect(() => {
    if (asking) cancelRef.current?.focus();
    else if (wasAsking.current) triggerRef.current?.focus();
    wasAsking.current = asking;
  }, [asking]);

  if (!asking) {
    return (
      <Button ref={triggerRef} variant={variant} size="sm" disabled={disabled} onClick={() => setAsking(true)}>
        {children}
      </Button>
    );
  }

  const onKeyDown = (event: KeyboardEvent) => {
    if (event.key === "Escape") setAsking(false);
  };

  return (
    <div role="group" aria-label={question} onKeyDown={onKeyDown} className="flex flex-wrap items-center gap-2">
      <span className="font-bold">{question}</span>
      <Button
        variant="dark"
        size="sm"
        onClick={() => {
          setAsking(false);
          onConfirm();
        }}
      >
        {confirmLabel}
      </Button>
      <Button ref={cancelRef} variant="secondary" size="sm" onClick={() => setAsking(false)}>
        {cancelLabel}
      </Button>
    </div>
  );
}
