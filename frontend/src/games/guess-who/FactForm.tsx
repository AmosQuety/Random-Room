import { useState } from "react";
import { fieldInputClass } from "../../components/styles";
import { Button } from "../../components/ui";

interface Props {
  disabled: boolean;
  onSubmit: (text: string) => void;
}

export function FactForm({ disabled, onSubmit }: Props) {
  const [text, setText] = useState("");
  const ready = text.trim().length > 0;
  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        if (ready && !disabled) onSubmit(text.trim());
      }}
      className="flex flex-col gap-3"
    >
      <label htmlFor="fact" className="font-mono text-sm font-bold uppercase tracking-widest">
        Something others might not know about you
      </label>
      <textarea
        id="fact"
        value={text}
        maxLength={200}
        rows={3}
        placeholder="I once ..."
        aria-describedby="fact-hint"
        onChange={(e) => setText(e.target.value)}
        className={`${fieldInputClass} py-2`}
      />
      <p id="fact-hint" className="text-sm text-muted">
        Keep it friendly. It stays secret until the group has guessed. {text.length}/200
      </p>
      <Button type="submit" variant="accent" size="lg" disabled={!ready || disabled}>
        Lock it in
      </Button>
    </form>
  );
}
