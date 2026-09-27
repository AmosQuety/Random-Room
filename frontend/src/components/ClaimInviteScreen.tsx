import { useState, type FormEvent } from "react";
import { ApiError, claimInvite } from "../lib/api";
import { navigate } from "../lib/router";

interface Props {
  slug: string;
  token: string;
}

export function ClaimInviteScreen({ slug, token }: Props) {
  const [pin, setPin] = useState("");
  const [confirmPin, setConfirmPin] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);
  const [claimedAs, setClaimedAs] = useState<string | null>(null);

  const mismatch = confirmPin.length > 0 && pin !== confirmPin;

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (pin.length < 4 || mismatch) return;
    setPending(true);
    setError(null);
    try {
      const { player } = await claimInvite(slug, token, pin);
      setClaimedAs(player);
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
      <main className="mx-auto flex min-h-dvh max-w-md flex-col justify-center gap-6 px-4 py-10 text-center">
        <p className="font-mono text-xs uppercase tracking-widest text-leaf">✅ PIN set</p>
        <h1 className="font-display text-4xl font-black">You're in, {claimedAs}</h1>
        <p className="text-lg text-muted">Keep that PIN to yourself - it's what proves it's you.</p>
        <button
          type="button"
          onClick={() => navigate(`/room/${slug}/join`)}
          className="min-h-14 rounded-lg border-2 border-ink bg-tomato px-6 text-lg font-black uppercase tracking-wide text-white shadow-ticket transition active:translate-x-1 active:translate-y-1 active:shadow-none"
        >
          Continue to the room
        </button>
      </main>
    );
  }

  return (
    <main className="mx-auto flex min-h-dvh max-w-md flex-col justify-center gap-8 px-4 py-10">
      <header>
        <p className="font-mono text-xs uppercase tracking-widest text-muted">One-time invite</p>
        <h1 className="mt-2 font-display text-4xl font-black leading-none">Set your PIN</h1>
        <p className="mt-4 text-lg text-muted">
          Pick a PIN only you know. Nobody else in the room, including the host, can see it - it's what stops anyone
          choosing on your behalf.
        </p>
      </header>

      <form onSubmit={submit} className="flex flex-col gap-6">
        <div>
          <label htmlFor="pin" className="mb-3 block font-mono text-sm uppercase tracking-widest">
            Your secret PIN
          </label>
          <input
            id="pin"
            type="password"
            inputMode="numeric"
            autoComplete="off"
            value={pin}
            onChange={(e) => setPin(e.target.value)}
            className="min-h-14 w-full rounded-lg border-2 border-ink bg-card px-4 text-2xl tracking-[0.4em]"
          />
          <p className="mt-2 text-sm text-muted">At least 4 characters.</p>
        </div>

        <div>
          <label htmlFor="confirm-pin" className="mb-3 block font-mono text-sm uppercase tracking-widest">
            Confirm PIN
          </label>
          <input
            id="confirm-pin"
            type="password"
            inputMode="numeric"
            autoComplete="off"
            value={confirmPin}
            onChange={(e) => setConfirmPin(e.target.value)}
            className="min-h-14 w-full rounded-lg border-2 border-ink bg-card px-4 text-2xl tracking-[0.4em]"
          />
          {mismatch && <p className="mt-2 text-sm font-semibold text-tomato">PINs don't match.</p>}
        </div>

        {error && (
          <p role="alert" className="rounded-lg border-2 border-tomato bg-card px-4 py-3 font-semibold text-tomato">
            {error}
          </p>
        )}

        <button
          type="submit"
          disabled={pin.length < 4 || mismatch || pending}
          className="min-h-14 rounded-lg border-2 border-ink bg-tomato px-6 text-lg font-black uppercase tracking-wide text-white shadow-ticket transition active:translate-x-1 active:translate-y-1 active:shadow-none disabled:cursor-not-allowed disabled:bg-muted disabled:shadow-none"
        >
          {pending ? "Saving..." : "Set my PIN"}
        </button>
      </form>
    </main>
  );
}
