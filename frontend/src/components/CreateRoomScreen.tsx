import { useState, type FormEvent } from "react";
import { GAME_LIST, GAMES } from "../games/registry";
import { ApiError, createRoom } from "../lib/api";
import { navigate } from "../lib/router";
import type { CreateRoomResult } from "../lib/types";
import { fieldInputClass, ListEditor } from "./ListEditor";

function RoomCreatedCard({ result }: { result: CreateRoomResult }) {
  const [copied, setCopied] = useState<string | null>(null);

  async function copy(player: string, url: string) {
    try {
      await navigator.clipboard.writeText(url);
      setCopied(player);
      setTimeout(() => setCopied(null), 2000);
    } catch {
      // Clipboard can be blocked; the link is still visible to select and copy by hand.
    }
  }

  return (
    <div className="flex flex-col gap-6 rounded-xl border-4 border-ink bg-card p-6 shadow-ticket">
      <div>
        <p className="font-mono text-xs uppercase tracking-widest text-leaf">✅ Room created</p>
        <h2 className="mt-1 font-display text-2xl font-black">Send each player their own link</h2>
        <p className="mt-2 text-sm text-muted">
          Each person opens their own link once to set a private PIN. Nobody else, including you, will see it - that's what
          stops anyone from choosing on someone else's behalf.
        </p>
      </div>
      <ul className="flex flex-col gap-3">
        {result.invites.map((invite) => {
          const url = `${window.location.origin}/room/${result.slug}/claim/${invite.inviteToken}`;
          return (
            <li key={invite.player} className="flex flex-wrap items-center justify-between gap-3 rounded-lg border-2 border-ink bg-paper px-4 py-3">
              <div className="min-w-0">
                <p className="font-display text-lg font-bold">{invite.player}</p>
                <p className="truncate font-mono text-xs text-muted">{url}</p>
              </div>
              <button
                type="button"
                onClick={() => copy(invite.player, url)}
                className="min-h-10 shrink-0 rounded-lg border-2 border-ink bg-card px-4 font-mono text-xs font-bold uppercase tracking-widest shadow-ticket-sm transition active:translate-x-0.5 active:translate-y-0.5 active:shadow-none"
              >
                {copied === invite.player ? "Copied!" : "Copy link"}
              </button>
            </li>
          );
        })}
      </ul>
      <button
        type="button"
        onClick={() => navigate(`/room/${result.slug}/join`)}
        className="min-h-12 self-start rounded-lg border-2 border-ink bg-tomato px-6 font-black uppercase tracking-wide text-white shadow-ticket-sm transition active:translate-x-0.5 active:translate-y-0.5 active:shadow-none"
      >
        Go to the room
      </button>
    </div>
  );
}

function GamePicker({ gameKey, onChange }: { gameKey: string; onChange: (key: string) => void }) {
  return (
    <fieldset>
      <legend className="mb-3 font-mono text-sm uppercase tracking-widest">What are you playing?</legend>
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        {GAME_LIST.map((game) => (
          <button
            key={game.key}
            type="button"
            aria-pressed={gameKey === game.key}
            onClick={() => onChange(game.key)}
            className={`flex flex-col gap-1 rounded-lg border-2 border-ink px-4 py-3 text-left shadow-ticket-sm transition active:translate-x-0.5 active:translate-y-0.5 active:shadow-none ${gameKey === game.key ? "bg-ink text-paper" : "bg-card"}`}
          >
            <span className="font-display text-lg font-bold">{game.name}</span>
            <span className={`text-sm ${gameKey === game.key ? "text-paper/80" : "text-muted"}`}>{game.description}</span>
          </button>
        ))}
      </div>
    </fieldset>
  );
}

export function CreateRoomScreen() {
  const [title, setTitle] = useState("");
  const [gameKey, setGameKey] = useState(GAME_LIST[0].key);
  // Registry-driven setup state is necessarily loosely typed here; each module's own toApiSetup/isSetupValid recovers the real type.
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const [setup, setSetup] = useState<any>(GAME_LIST[0].defaultSetup);
  const [players, setPlayers] = useState(["", ""]);
  const [host, setHost] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);
  const [result, setResult] = useState<CreateRoomResult | null>(null);

  const game = GAMES[gameKey];
  const cleanPlayers = players.map((p) => p.trim()).filter(Boolean);

  function selectGame(key: string) {
    setGameKey(key);
    setSetup(GAMES[key].defaultSetup);
  }

  async function submit(event: FormEvent) {
    event.preventDefault();
    const hostPlayer = host || cleanPlayers[0];
    if (!game.isSetupValid(setup) || cleanPlayers.length < 2 || !hostPlayer) return;

    setPending(true);
    setError(null);
    try {
      setResult(await createRoom(title.trim(), game.key, game.toApiSetup(setup), cleanPlayers, hostPlayer));
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Could not reach the server. Check your connection and try again.");
    } finally {
      setPending(false);
    }
  }

  if (result) return <RoomCreatedCard result={result} />;

  return (
    <form onSubmit={submit} className="flex flex-col gap-6">
      <div>
        <label htmlFor="title" className="mb-2 block font-mono text-sm uppercase tracking-widest">
          What are you deciding?
        </label>
        <input
          id="title"
          value={title}
          onChange={(e) => setTitle(e.target.value)}
          placeholder="Which game tonight?"
          className={fieldInputClass}
        />
      </div>

      <GamePicker gameKey={gameKey} onChange={selectGame} />

      <game.SetupForm value={setup} onChange={setSetup} />

      <ListEditor
        legend="Players"
        hint="Everyone who gets a turn. Each one gets their own private invite link."
        items={players}
        onChange={setPlayers}
        placeholder={(i) => `Player ${i + 1}`}
      />

      <div>
        <label htmlFor="host" className="mb-2 block font-mono text-sm uppercase tracking-widest">
          Who's hosting?
        </label>
        <p className="-mt-1 mb-2 text-sm text-muted">The host starts and ends rounds. Defaults to the first player.</p>
        <select id="host" value={host} onChange={(e) => setHost(e.target.value)} className={fieldInputClass}>
          <option value="">{cleanPlayers[0] || "First player"}</option>
          {cleanPlayers.map((p) => (
            <option key={p} value={p}>
              {p}
            </option>
          ))}
        </select>
      </div>

      {error && (
        <p role="alert" className="rounded-lg border-2 border-tomato bg-card px-4 py-3 font-semibold text-tomato">
          {error}
        </p>
      )}

      <button
        type="submit"
        disabled={pending}
        className="min-h-14 rounded-lg border-2 border-ink bg-tomato px-6 text-lg font-black uppercase tracking-wide text-white shadow-ticket transition active:translate-x-1 active:translate-y-1 active:shadow-none disabled:cursor-not-allowed disabled:bg-muted disabled:shadow-none"
      >
        {pending ? "Creating..." : "Create the room"}
      </button>
    </form>
  );
}
