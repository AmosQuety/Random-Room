import { useState } from "react";
import { CheckIcon, CopyIcon } from "./icons";
import { Button } from "./ui";

interface Props {
  code: string;
  /** What the code is called in the heading, so the same card works after creating a room and after replacing a code. */
  heading?: string;
}

/** Shows a recovery code once, with what it is for and why to keep it somewhere safe. */
export function RecoveryCodeCard({ code, heading = "Your recovery code" }: Props) {
  const [copied, setCopied] = useState(false);

  async function copy() {
    try {
      await navigator.clipboard.writeText(code);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      // Clipboard can be blocked; the code is on screen to copy by hand.
    }
  }

  return (
    <section aria-label={heading} className="flex flex-col gap-3 rounded-lg border-2 border-ink bg-mustard-soft p-4">
      <h3 className="font-display text-xl font-bold">{heading}</h3>
      <p className="select-all break-all font-mono text-2xl font-bold tracking-widest" aria-label={`Code: ${code.split("").join(" ")}`}>
        {code}
      </p>
      <p className="text-sm">
        If you forget your PIN, this code gets you back in as host from the join page. It is shown only once, so save it
        somewhere safe, such as a note on your phone. Anyone who has it can take over as host, so do not share it.
      </p>
      <div>
        <Button size="sm" variant="secondary" onClick={copy}>
          {copied ? <CheckIcon className="size-4" /> : <CopyIcon className="size-4" />}
          {copied ? "Copied" : "Copy code"}
        </Button>
        <span role="status" className="sr-only">
          {copied ? "Recovery code copied" : ""}
        </span>
      </div>
    </section>
  );
}
