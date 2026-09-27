import { useState, type FormEvent } from "react";
import { CreateRoomScreen } from "./CreateRoomScreen";
import { navigate } from "../lib/router";

function JoinByCode() {
  const [code, setCode] = useState("");

  function submit(event: FormEvent) {
    event.preventDefault();
    const slug = code.trim().toLowerCase();
    if (slug) navigate(`/room/${slug}/join`);
  }

  return (
    <form onSubmit={submit} className="flex flex-col gap-3 rounded-xl border-2 border-dashed border-ink px-4 py-4">
      <p className="font-mono text-xs uppercase tracking-widest text-muted">Already have a room code?</p>
      <div className="flex gap-2">
        <input
          value={code}
          onChange={(e) => setCode(e.target.value)}
          placeholder="e.g. a1b2c3"
          className="min-h-12 w-full rounded-lg border-2 border-ink bg-card px-3 font-mono text-lg tracking-widest"
        />
        <button
          type="submit"
          disabled={!code.trim()}
          className="min-h-12 shrink-0 rounded-lg border-2 border-ink bg-card px-5 font-black uppercase tracking-wide shadow-ticket-sm transition active:translate-x-0.5 active:translate-y-0.5 active:shadow-none disabled:cursor-not-allowed disabled:opacity-50"
        >
          Go
        </button>
      </div>
    </form>
  );
}

export function HomeScreen() {
  return (
    <main className="mx-auto flex min-h-dvh max-w-2xl flex-col justify-center gap-8 px-4 py-10">
      <header>
        <p className="font-mono text-xs uppercase tracking-widest text-muted">The Playground</p>
        <h1 className="mt-2 font-display text-5xl font-black leading-none">Games for your group</h1>
        <p className="mt-4 text-lg text-muted">Icebreakers and game nights, run fair and server-side. Set up a room and share the code.</p>
      </header>

      <CreateRoomScreen />
      <JoinByCode />
    </main>
  );
}
