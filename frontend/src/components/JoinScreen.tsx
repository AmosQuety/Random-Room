import { useCallback, useEffect, useState, type FormEvent } from "react";
import { GAMES } from "../games/registry";
import { ApiError, getRoomPreview, join } from "../lib/api";
import type { RoomPreview, Session } from "../lib/types";
import { Avatar } from "./Avatar";
import { ArrowRightIcon, CheckIcon } from "./icons";
import { Shell } from "./Shell";
import { Alert, AppLink, Button, EmptyState, Eyebrow, Field, Skeleton } from "./ui";
import { buttonClass, inputClass, sectionHeadingClass } from "./styles";

interface Props {
  slug: string;
  onJoined: (session: Session) => void;
}

type Load = { status: "loading" } | { status: "missing" } | { status: "failed" } | { status: "ready"; preview: RoomPreview };

function JoinSkeleton() {
  return (
    <div role="status" aria-label="Loading the room" className="flex flex-col gap-6">
      <Skeleton className="h-14 w-3/4" />
      <Skeleton className="h-6 w-1/2" />
      <div className="grid grid-cols-2 gap-3">
        <Skeleton className="h-16" />
        <Skeleton className="h-16" />
        <Skeleton className="h-16" />
        <Skeleton className="h-16" />
      </div>
      <Skeleton className="h-14" />
    </div>
  );
}

export function JoinScreen({ slug, onJoined }: Props) {
  const [load, setLoad] = useState<Load>({ status: "loading" });
  const [player, setPlayer] = useState<string | null>(null);
  const [pin, setPin] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);

  const fetchPreview = useCallback(() => {
    getRoomPreview(slug)
      .then((preview) => setLoad({ status: "ready", preview }))
      .catch((e) => setLoad(e instanceof ApiError && e.status === 404 ? { status: "missing" } : { status: "failed" }));
  }, [slug]);

  useEffect(() => {
    fetchPreview();
  }, [fetchPreview]);

  function retry() {
    setLoad({ status: "loading" });
    fetchPreview();
  }

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

  if (load.status === "loading") {
    return (
      <Shell>
        <JoinSkeleton />
      </Shell>
    );
  }

  if (load.status === "missing") {
    return (
      <Shell>
        <EmptyState
          tone="error"
          title={`No room at "${slug}"`}
          action={
            <>
              <AppLink href="/" className={buttonClass("primary")}>
                Back to home
              </AppLink>
            </>
          }
        >
          <p>Double-check the link, or ask whoever created the room to resend it.</p>
        </EmptyState>
      </Shell>
    );
  }

  if (load.status === "failed") {
    return (
      <Shell>
        <EmptyState
          tone="error"
          title="Couldn't reach the room"
          action={
            <Button variant="primary" onClick={retry}>
              Try again
            </Button>
          }
        >
          <p>Check your connection. This can take a moment if the server was asleep.</p>
        </EmptyState>
      </Shell>
    );
  }

  const { preview } = load;
  const game = GAMES[preview.gameType];
  const claimedCount = preview.players.filter((p) => p.claimed).length;

  return (
    <Shell accent={game?.accent}>
      <div className="flex flex-col gap-8">
        <header className="flex flex-col gap-3">
          <Eyebrow className="flex items-center gap-2">
            {game && <game.Glyph className="size-6" />}
            {game ? game.name : "Game room"}
          </Eyebrow>
          <h1 className="font-display text-5xl font-black leading-[0.95] tracking-tight">{preview.title || "Untitled room"}</h1>
          <p className="text-lg text-muted">{game ? game.hook : "Pick who you are and jump in."}</p>
          <p className="flex items-center gap-2 font-mono text-xs font-bold uppercase tracking-widest">
            <span className="rounded-md border-2 border-ink bg-accent-soft px-2 py-1">
              {claimedCount} of {preview.players.length} players ready
            </span>
          </p>
        </header>

        <form onSubmit={submit} className="flex flex-col gap-6">
          <fieldset>
            <legend className={`mb-3 ${sectionHeadingClass}`}>1. Select who you are</legend>
            <div className="grid grid-cols-2 gap-3">
              {preview.players.map(({ name, claimed }) => (
                <label
                  key={name}
                  className={`relative flex min-h-16 items-center gap-3 rounded-lg border-2 border-ink px-3 py-2 text-left text-lg font-bold transition has-[:focus-visible]:outline-3 has-[:focus-visible]:outline-offset-3 has-[:focus-visible]:outline-sky ${
                    claimed ? "cursor-pointer" : "cursor-not-allowed bg-paper-deep"
                  } ${player === name ? "bg-ink text-paper shadow-ticket-sm" : claimed ? "bg-card shadow-ticket-sm hover:-translate-y-0.5" : ""}`}
                >
                  <input
                    type="radio"
                    name="player"
                    value={name}
                    disabled={!claimed}
                    checked={player === name}
                    onChange={() => setPlayer(name)}
                    className="sr-only"
                  />
                  <Avatar name={name} />
                  <span className="min-w-0 flex-1 truncate">
                    {name}
                    {!claimed && <span className="block font-mono text-xs font-normal uppercase text-muted">Not joined yet</span>}
                  </span>
                  {player === name && <CheckIcon className="absolute right-1.5 top-1.5 size-4 text-mustard" />}
                </label>
              ))}
            </div>
            {claimedCount < preview.players.length && (
              <p className="mt-3 text-sm text-muted">Greyed-out names need to open their invite link and set a PIN first.</p>
            )}
          </fieldset>

          <Field id="pin" label="2. Your secret PIN" hint="This stops someone else joining as you.">
            <input
              id="pin"
              type="password"
              inputMode="numeric"
              autoComplete="off"
              value={pin}
              onChange={(e) => setPin(e.target.value)}
              aria-describedby="pin-hint"
              className={`${inputClass} min-h-14 text-2xl tracking-[0.4em]`}
            />
          </Field>

          {error && <Alert>{error}</Alert>}

          <Button type="submit" variant="accent" size="lg" disabled={!player || pin.length < 4 || pending}>
            {pending ? "Joining..." : "Enter the room"} <ArrowRightIcon />
          </Button>
        </form>
      </div>
    </Shell>
  );
}
