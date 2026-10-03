import { useState } from "react";
import { claimUrl } from "../lib/invites";
import { CheckIcon, CopyIcon } from "./icons";
import { Button } from "./ui";

interface Props {
  slug: string;
  player: string;
  token: string;
}

/** What the host can do with one player's private link. */
export function InviteActions({ slug, player, token }: Props) {
  const [copied, setCopied] = useState(false);

  async function copy() {
    try {
      await navigator.clipboard.writeText(claimUrl(slug, token));
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      // Clipboard can be blocked; the link is still visible to select and copy by hand.
    }
  }

  return (
    <>
      <Button size="sm" variant="secondary" onClick={copy}>
        {copied ? <CheckIcon className="size-4" /> : <CopyIcon className="size-4" />}
        {copied ? "Copied" : "Copy link"}{" "}
        <span className="sr-only">for {player}</span>
      </Button>
      <span role="status" className="sr-only">
        {copied ? `Link for ${player} copied` : ""}
      </span>
    </>
  );
}
