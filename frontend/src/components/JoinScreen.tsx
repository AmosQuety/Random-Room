import { useEffect, useState, type FormEvent } from "react";
import { ApiError, getRoomPreview, join } from "../lib/api";
import type { RoomPreview, Session } from "../lib/types";
import { Avatar } from "./Avatar";

interface Props {
  slug: string;
  onJoined: (session: Session) => void;
}

export function JoinScreen({ slug, onJoined }: Props) {
  const [preview, setPreview] = useState<RoomPreview | null>(null);
  const [loadError, setLoadError] = useState(false);
  const [player, setPlayer] = useState<string | null>(null);
  const [pin, setPin] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);

  useEffect(() => {
    getRoomPreview(slug)
      .then(setPreview)
      .catch(() => setLoadError(true));
  }, [slug]);

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (!player) return;
    setPending(true);
    setError(null);
    try {
      onJoined(await join(slug, player, pin));
    } catch (e) {
      setError(e instanceof ApiError && e.status === 429 ? "Too many attempts. Wait a minute and try again." : "That name and PIN don't match.");
      setPending(false);
    }
  }

  if (loadError) {
    return (
      <main className="mx-auto flex min-h-dvh max-w-md flex-col justify-center gap-4 px-4 py-10 text-center">
        <p className="font-mono text-xs uppercase tracking-widest text-tomato">Not found</p>
        <h1 className="font-display text-3xl font-black">No room at "{slug}"</h1>
        <p className="text-muted">Double-check the link, or ask whoever created the room to resend it.</p>
      </main>
    );
  }

  if (!preview) return <p className="p-6 font-mono text-muted">Loading the room...</p>;

  return (
    <main className="mx-auto flex min-h-dvh max-w-md flex-col justify-center gap-8 px-4 py-10">
      <header>
        <p className="font-mono text-xs uppercase tracking-widest text-muted">Choice Maker</p>
        <h1 className="mt-2 font-display text-5xl font-black leading-none">{preview.title}</h1>
        <p className="mt-4 text-lg text-muted">Nobody votes here. The server rolls, everyone sees it.</p>
      </header>

      <form onSubmit={submit} className="flex flex-col gap-6">
        <fieldset>
          <legend className="mb-3 font-mono text-sm uppercase tracking-widest">1. Select who you are</legend>
          <div className="grid grid-cols-2 gap-3">
            {preview.players.map(({ name, claimed }) => (
              <button
                key={name}
                type="button"
                disabled={!claimed}
                aria-pressed={player === name}
                title={claimed ? undefined : "Ask them to open their invite link and set a PIN first"}
                onClick={() => setPlayer(name)}
                className={`flex min-h-14 items-center gap-3 rounded-lg border-2 border-ink px-3 text-left text-lg font-bold shadow-ticket-sm transition active:translate-x-0.5 active:translate-y-0.5 active:shadow-none disabled:cursor-not-allowed disabled:opacity-40 disabled:shadow-none ${player === name ? "bg-ink text-paper" : "bg-card"}`}
              >
                <Avatar name={name} />
                <span className="min-w-0 truncate">
                  {name}
                  {!claimed && <span className="block font-mono text-xs font-normal uppercase text-muted">Not joined yet</span>}
                </span>
              </button>
            ))}
          </div>
        </fieldset>

        <div>
          <label htmlFor="pin" className="mb-3 block font-mono text-sm uppercase tracking-widest">
            2. Your secret PIN
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
          <p className="mt-2 text-sm text-muted">This stops someone else joining as you.</p>
        </div>

        {error && (
          <p role="alert" className="rounded-lg border-2 border-tomato bg-card px-4 py-3 font-semibold text-tomato">
            {error}
          </p>
        )}

        <button
          type="submit"
          disabled={!player || pin.length < 4 || pending}
          className="min-h-14 rounded-lg border-2 border-ink bg-tomato px-6 text-lg font-black uppercase tracking-wide text-white shadow-ticket transition active:translate-x-1 active:translate-y-1 active:shadow-none disabled:cursor-not-allowed disabled:bg-muted disabled:shadow-none"
        >
          {pending ? "Joining..." : "Enter the room"}
        </button>
      </form>
    </main>
  );
}
