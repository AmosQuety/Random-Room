import { useState, type FormEvent } from "react";
import { ApiError, claimInvite, join } from "../lib/api";
import { redirect } from "../lib/router";
import type { Session } from "../lib/types";
import { ArrowRightIcon, CheckIcon } from "./icons";
import { Shell } from "./Shell";
import { Alert, Button, Card, Eyebrow, Field } from "./ui";
import { inputClass } from "./styles";

interface Props {
  slug: string;
  token: string;
  onJoined: (session: Session) => void;
}

const pinInputClass = `${inputClass} min-h-14 text-2xl tracking-[0.4em]`;

export function ClaimInviteScreen({ slug, token, onJoined }: Props) {
  const [pin, setPin] = useState("");
  const [confirmPin, setConfirmPin] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);
  const [claimedAs, setClaimedAs] = useState<string | null>(null);

  const mismatch = confirmPin.length > 0 && pin !== confirmPin;

  // The player just typed this PIN on this device, so sign them in rather than making them retype it.
  // If that fails the PIN is still saved and the "Continue" button leads to the manual join form.
  async function signInWithNewPin(player: string) {
    try {
      onJoined(await join(slug, player, pin));
      redirect(`/room/${slug}`);
    } catch {
      setPending(false);
    }
  }

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (pin.length < 4 || mismatch) return;
    setPending(true);
    setError(null);
    try {
      const { player } = await claimInvite(slug, token, pin);
      setClaimedAs(player);
      await signInWithNewPin(player);
    } catch (e) {
      setError(
        e instanceof ApiError && e.status === 404
          ? "This invite link is invalid or has already been used."
          : "Could not reach the server. Check your connection and try again.",
      );
      setPending(false);
    }
  }

  if (claimedAs) {
    return (
      <Shell>
        <Card className="animate-stamp flex flex-col items-center gap-5 text-center">
          <span className="grid size-16 place-items-center rounded-full border-2 border-ink bg-leaf text-white">
            <CheckIcon className="size-8" />
          </span>
          <Eyebrow className="!text-leaf">PIN set</Eyebrow>
          <h1 className="font-display text-4xl font-black leading-tight">You're in, {claimedAs}</h1>
          <p className="text-lg text-muted">Keep that PIN to yourself - it's what proves it's you.</p>
          <Button variant="primary" size="lg" onClick={() => redirect(`/room/${slug}/join`)}>
            Continue to the room <ArrowRightIcon />
          </Button>
        </Card>
      </Shell>
    );
  }

  return (
    <Shell>
      <div className="flex flex-col gap-8">
        <header>
          <Eyebrow>One-time invite</Eyebrow>
          <h1 className="mt-2 font-display text-5xl font-black leading-[0.95] tracking-tight">Set your PIN</h1>
          <p className="mt-4 text-lg text-muted">
            Pick a PIN only you know. Nobody else in the room, including the host, can see it - it's what stops anyone
            playing on your behalf.
          </p>
        </header>

        <form onSubmit={submit} className="flex flex-col gap-6">
          <Field id="pin" label="Your secret PIN" hint="At least 4 characters.">
            <input
              id="pin"
              type="password"
              inputMode="numeric"
              autoComplete="off"
              value={pin}
              onChange={(e) => setPin(e.target.value)}
              aria-describedby="pin-hint"
              className={pinInputClass}
            />
          </Field>

          <Field id="confirm-pin" label="Confirm PIN" error={mismatch ? "PINs don't match." : null}>
            <input
              id="confirm-pin"
              type="password"
              inputMode="numeric"
              autoComplete="off"
              value={confirmPin}
              onChange={(e) => setConfirmPin(e.target.value)}
              aria-invalid={mismatch}
              className={pinInputClass}
            />
          </Field>

          {error && <Alert>{error}</Alert>}

          <Button type="submit" variant="primary" size="lg" disabled={pin.length < 4 || mismatch || pending}>
            {pending ? "Saving..." : "Set my PIN"}
          </Button>
        </form>
      </div>
    </Shell>
  );
}
