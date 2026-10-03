import { Suspense, useEffect, useRef, useState, type FormEvent } from "react";
import { GAME_LIST, GAMES } from "../games/registry";
import { ApiError, createRoom } from "../lib/api";
import { claimPath, claimUrl } from "../lib/invites";
import { navigate } from "../lib/router";
import { playerCountIssue } from "../lib/players";
import type { CreateRoomResult } from "../lib/types";
import { GamePicker } from "./GamePicker";
import { InviteActions } from "./InviteActions";
import { ArrowLeftIcon, ArrowRightIcon, CheckIcon, CopyIcon, PlayIcon } from "./icons";
import { ListEditor } from "./ListEditor";
import { Alert, Button, Card, Eyebrow, Field, Skeleton } from "./ui";
import { inputClass } from "./styles";

const STEPS = ["Game", "Setup", "Players"] as const;

function Stepper({ step, onGoTo }: { step: number; onGoTo: (step: number) => void }) {
  return (
    <ol aria-label="Steps" className="flex items-center gap-2">
      {STEPS.map((label, i) => {
        const done = i < step;
        const current = i === step;
        const badge = (
          <span
            className={`grid size-8 shrink-0 place-items-center rounded-full border-2 border-ink font-mono text-sm font-bold ${
              done ? "bg-leaf text-white" : current ? "bg-ink text-paper" : "bg-card text-muted"
            }`}
          >
            {done ? <CheckIcon className="size-4" /> : i + 1}
          </span>
        );
        return (
          <li key={label} aria-current={current ? "step" : undefined} className="flex flex-1 items-center gap-2 last:flex-none">
            {done ? (
              <button type="button" onClick={() => onGoTo(i)} className="flex min-h-11 items-center gap-2 rounded-lg">
                {badge}
                <span className="font-mono text-xs font-bold uppercase tracking-widest">
                  <span className="sr-only">Go back to </span>
                  {label}
                </span>
              </button>
            ) : (
              <span className="flex min-h-11 items-center gap-2">
                {badge}
                <span className={`font-mono text-xs font-bold uppercase tracking-widest ${current ? "" : "text-muted"}`}>{label}</span>
              </span>
            )}
            {i < STEPS.length - 1 && <span aria-hidden="true" className="h-0.5 flex-1 bg-ink/30" />}
          </li>
        );
      })}
    </ol>
  );
}

function RoomCreatedCard({ result, hostPlayer }: { result: CreateRoomResult; hostPlayer: string }) {
  const [copied, setCopied] = useState<string | null>(null);
  const urlFor = (token: string) => claimUrl(result.slug, token);

  async function copy(label: string, text: string) {
    try {
      await navigator.clipboard.writeText(text);
      setCopied(label);
      setTimeout(() => setCopied(null), 2000);
    } catch {
      // Clipboard can be blocked; the link is still visible to select and copy by hand.
    }
  }

  const everything = result.invites.map((invite) => `${invite.player}: ${urlFor(invite.inviteToken)}`).join("\n");

  return (
    <Card className="animate-rise !p-0 overflow-hidden">
      <div className="flex flex-col gap-2 bg-leaf-soft p-5 sm:p-6">
        <Eyebrow className="flex items-center gap-1.5 !text-leaf">
          <CheckIcon className="size-4" /> Room created
        </Eyebrow>
        <h3 className="font-display text-3xl font-black leading-tight">Send each player their own link</h3>
        <p className="text-muted">
          Each person opens their own link once to set a private PIN, and is taken straight into the room. Nobody else,
          including you, will see it - that's what stops anyone from playing on someone else's behalf. Start with your own
          link, since the host's seat has to be claimed before the game can begin.
        </p>
      </div>
      <div className="ticket-perforation" />
      <div className="flex flex-col gap-5 p-5 sm:p-6">
        <ul className="flex flex-col gap-3">
          {result.invites.map((invite) => (
            <li
              key={invite.player}
              className="flex flex-wrap items-center justify-between gap-3 rounded-lg border-2 border-ink bg-paper px-4 py-3"
            >
              <div className="min-w-0 flex-1">
                <p className="font-display text-lg font-bold">
                  {invite.player}
                  {invite.player === hostPlayer && (
                    <span className="ml-2 rounded-md border-2 border-ink bg-accent-soft px-1.5 py-0.5 align-middle font-mono text-xs uppercase tracking-widest">
                      You, host
                    </span>
                  )}
                </p>
                <p className="truncate font-mono text-xs text-muted">{urlFor(invite.inviteToken)}</p>
              </div>
              {invite.player === hostPlayer && (
                <Button size="sm" variant="primary" onClick={() => navigate(claimPath(result.slug, invite.inviteToken))}>
                  Set my PIN <ArrowRightIcon className="size-4" />
                </Button>
              )}
              <InviteActions slug={result.slug} player={invite.player} token={invite.inviteToken} />
            </li>
          ))}
        </ul>
        <div className="flex flex-wrap gap-3">
          <Button variant="secondary" onClick={() => navigate(`/room/${result.slug}/join`)}>
            Already set a PIN? Go to the room <ArrowRightIcon />
          </Button>
          <Button variant="secondary" onClick={() => copy("everyone", everything)}>
            <CopyIcon className="size-4" />
            {copied === "everyone" ? "Copied all" : "Copy all links"}
          </Button>
        </div>
      </div>
    </Card>
  );
}

function hasDuplicates(names: string[]): boolean {
  const seen = new Set(names.map((n) => n.toLowerCase()));
  return seen.size !== names.length;
}

export function CreateRoomScreen() {
  const [step, setStep] = useState(0);
  const [gameKey, setGameKey] = useState<string | null>(null);
  // Registry-driven setup state is necessarily loosely typed here; each module's own toApiSetup/isSetupValid recovers the real type.
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const [setup, setSetup] = useState<any>(null);
  const [title, setTitle] = useState("");
  const [players, setPlayers] = useState(["", ""]);
  const [host, setHost] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);
  const [result, setResult] = useState<CreateRoomResult | null>(null);
  const [createdHost, setCreatedHost] = useState("");

  const headingRef = useRef<HTMLHeadingElement>(null);
  const shownStep = useRef(step);
  useEffect(() => {
    // Move focus to the new step's heading so keyboard and screen-reader users land in the right place.
    if (shownStep.current === step) return;
    shownStep.current = step;
    headingRef.current?.focus();
    headingRef.current?.scrollIntoView({ block: "start", behavior: "smooth" });
  }, [step]);

  const game = gameKey ? GAMES[gameKey] : null;
  const cleanPlayers = players.map((p) => p.trim()).filter(Boolean);
  const countIssue = game ? playerCountIssue(cleanPlayers.length, game) : null;
  const duplicateIssue = hasDuplicates(cleanPlayers) ? "Each player needs a different name." : null;
  const playersIssue = countIssue ?? duplicateIssue;
  const setupReady = game ? game.isSetupValid(setup) : false;

  function selectGame(key: string) {
    if (key === gameKey) return;
    setGameKey(key);
    setSetup(GAMES[key].defaultSetup);
  }

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (!game || step !== STEPS.length - 1) return;
    const hostPlayer = host && cleanPlayers.includes(host) ? host : cleanPlayers[0];
    if (!setupReady || playersIssue || !hostPlayer) return;

    setPending(true);
    setError(null);
    try {
      setResult(await createRoom(title.trim() || `${game.name} night`, game.key, game.toApiSetup(setup), cleanPlayers, hostPlayer));
      setCreatedHost(hostPlayer);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Could not reach the server. Check your connection and try again.");
    } finally {
      setPending(false);
    }
  }

  if (result) return <RoomCreatedCard result={result} hostPlayer={createdHost} />;

  const stepReady = step === 0 ? game !== null : step === 1 ? setupReady : !playersIssue;
  const setupBlocker = game?.setupIssue?.(setup) ?? "Finish the setup to continue.";
  const blocker = step === 0 ? "Pick a game to continue." : step === 1 ? setupBlocker : playersIssue;
  const isLast = step === STEPS.length - 1;

  return (
    <form onSubmit={submit} data-accent={game?.accent} className="flex flex-col gap-6" aria-labelledby="create-heading">
      <Stepper step={step} onGoTo={setStep} />

      <div className="flex flex-col gap-6">
        {step === 0 && (
          <>
            <div>
              <h3 id="create-heading" ref={headingRef} tabIndex={-1} className="font-display text-3xl font-black leading-tight outline-none">
                Pick a game
              </h3>
              <p className="mt-1 text-muted">Everything runs on the server, so nobody can peek or cheat. Choose what the group is playing.</p>
            </div>
            <GamePicker games={GAME_LIST} selectedKey={gameKey} onSelect={selectGame} />
          </>
        )}

        {step === 1 && game && (
          <>
            <div className="flex items-center gap-4 rounded-xl border-2 border-ink bg-accent-soft p-4">
              <span className="grid size-14 shrink-0 place-items-center rounded-lg border-2 border-ink bg-card">
                <game.Glyph className="size-10" />
              </span>
              <div>
                <h3 id="create-heading" ref={headingRef} tabIndex={-1} className="font-display text-2xl font-black leading-tight outline-none">
                  Set up {game.name}
                </h3>
                <p className="text-sm">{game.description}</p>
              </div>
            </div>

            <Field id="room-title" label="Room name" hint={`Shown to everyone who joins. Leave blank for "${game.name} night".`}>
              <input
                id="room-title"
                value={title}
                maxLength={80}
                onChange={(e) => setTitle(e.target.value)}
                placeholder="Friday game night"
                aria-describedby="room-title-hint"
                className={inputClass}
              />
            </Field>

            <Suspense fallback={<Skeleton className="h-48" />}>
              <game.SetupForm value={setup} onChange={setSetup} />
            </Suspense>
          </>
        )}

        {step === 2 && game && (
          <>
            <div>
              <h3 id="create-heading" ref={headingRef} tabIndex={-1} className="font-display text-3xl font-black leading-tight outline-none">
                Who's playing?
              </h3>
              <p className="mt-1 text-muted">Each person gets their own private invite link.</p>
            </div>

            <ListEditor
              legend="Players"
              hint={`Add everyone who's joining ${game.name}.`}
              items={players}
              onChange={setPlayers}
              placeholder={(i) => `Player ${i + 1}`}
              maxItems={12}
              maxLength={32}
              addLabel="Add a player"
            />

            <Field id="host" label="Who's hosting?" hint="The host starts and ends games. Defaults to the first player.">
              <select id="host" value={host} onChange={(e) => setHost(e.target.value)} aria-describedby="host-hint" className={inputClass}>
                <option value="">{cleanPlayers[0] || "First player"}</option>
                {cleanPlayers.map((p) => (
                  <option key={p} value={p}>
                    {p}
                  </option>
                ))}
              </select>
            </Field>
          </>
        )}

        {error && <Alert>{error}</Alert>}
      </div>

      <div className="sticky bottom-0 z-10 -mx-4 border-t-2 border-ink bg-paper px-4 py-3">
        <div className="mx-auto flex max-w-5xl flex-col gap-2">
          {!stepReady && blocker && (
            <p id="step-blocker" role="status" className="text-sm font-semibold text-tomato">
              {blocker}
            </p>
          )}
          <div className="flex gap-3">
            {step > 0 && (
              <Button variant="secondary" size="lg" onClick={() => setStep(step - 1)}>
                <ArrowLeftIcon /> Back
              </Button>
            )}
            {isLast ? (
              <Button type="submit" variant="accent" size="lg" className="flex-1 sm:ml-auto sm:flex-none" disabled={!stepReady || pending} aria-describedby={stepReady ? undefined : "step-blocker"}>
                <PlayIcon className="size-4" />
                {pending ? "Creating..." : "Create the room"}
              </Button>
            ) : (
              <Button variant="accent" size="lg" className="flex-1 sm:ml-auto sm:flex-none" disabled={!stepReady} aria-describedby={stepReady ? undefined : "step-blocker"} onClick={() => setStep(step + 1)}>
                {step === 0 && game ? `Continue with ${game.name}` : "Next"} <ArrowRightIcon />
              </Button>
            )}
          </div>
        </div>
      </div>
    </form>
  );
}
