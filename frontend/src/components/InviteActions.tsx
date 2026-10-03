import { lazy, Suspense, useState } from "react";
import { claimUrl } from "../lib/invites";
import { CheckIcon, CopyIcon, QrIcon, ShareIcon } from "./icons";
import { Button } from "./ui";

// Loaded only when a host asks for a code, so it costs nothing on the pages everyone opens.
const InviteQr = lazy(() => import("./InviteQr"));

interface Props {
  slug: string;
  player: string;
  token: string;
  roomTitle: string;
}

const canShare = () => typeof navigator !== "undefined" && typeof navigator.share === "function";

/** What the host can do with one player's private link: share it, copy it, or show a code to scan. */
export function InviteActions({ slug, player, token, roomTitle }: Props) {
  const [copied, setCopied] = useState(false);
  const [showQr, setShowQr] = useState(false);
  const url = claimUrl(slug, token);

  async function copy() {
    try {
      await navigator.clipboard.writeText(url);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      // Clipboard can be blocked; the link is still visible to select and copy by hand.
    }
  }

  async function share() {
    try {
      await navigator.share({
        title: roomTitle || "Join my game",
        text: `${player}, here is your private link to join "${roomTitle || "the game"}". Open it once to choose your PIN.`,
        url,
      });
    } catch (e) {
      // Closing the share sheet is not a failure. Anything else (no apps, blocked) falls back to copying.
      if (!(e instanceof DOMException && e.name === "AbortError")) await copy();
    }
  }

  return (
    <>
      {canShare() && (
        <Button size="sm" variant="primary" onClick={share}>
          <ShareIcon className="size-4" /> Share{" "}
          <span className="sr-only">link with {player}</span>
        </Button>
      )}
      <Button size="sm" variant="secondary" onClick={copy}>
        {copied ? <CheckIcon className="size-4" /> : <CopyIcon className="size-4" />}
        {copied ? "Copied" : "Copy link"}{" "}
        <span className="sr-only">for {player}</span>
      </Button>
      <Button size="sm" variant="secondary" aria-expanded={showQr} onClick={() => setShowQr((open) => !open)}>
        <QrIcon className="size-4" /> {showQr ? "Hide code" : "Show code"}{" "}
        <span className="sr-only">for {player}</span>
      </Button>
      <span role="status" className="sr-only">
        {copied ? `Link for ${player} copied` : ""}
      </span>
      {showQr && (
        <div className="flex basis-full flex-col items-start gap-2">
          <Suspense fallback={<p className="text-sm text-muted">Making the code...</p>}>
            <InviteQr value={url} label={`QR code for ${player}'s private link`} />
          </Suspense>
          <p className="text-sm text-muted">
            Let {player} scan this with their phone camera. It is their private link, so show it only to them.
          </p>
        </div>
      )}
    </>
  );
}
