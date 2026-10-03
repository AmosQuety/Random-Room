import { useState, type FormEvent } from "react";
import { ApiError, recoverHost } from "../lib/api";
import { claimPath } from "../lib/invites";
import type { HostRecovery } from "../lib/types";
import { ArrowRightIcon } from "./icons";
import { RecoveryCodeCard } from "./RecoveryCodeCard";
import { Shell } from "./Shell";
import { Alert, AppLink, Button, Card, Eyebrow, Field } from "./ui";
import { inputClass } from "./styles";
import { redirect } from "../lib/router";

interface Props {
  slug: string;
}

function messageFor(error: unknown): string {
  if (error instanceof ApiError && error.status === 429) return "Too many attempts. Wait a minute and try again.";
  if (error instanceof ApiError && (error.status === 403 || error.status === 409)) return error.message;
  return "Could not reach the server. Check your connection and try again.";
}

/** For a host who forgot their PIN: the recovery code signs the old device out and lets them choose a new PIN. */
export function RecoverHostScreen({ slug }: Props) {
  const [code, setCode] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);
  const [recovery, setRecovery] = useState<HostRecovery | null>(null);
  const [saved, setSaved] = useState(false);

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (code.trim().length === 0) return;
    setPending(true);
    setError(null);
    try {
      setRecovery(await recoverHost(slug, code));
    } catch (e) {
      setError(messageFor(e));
    } finally {
      setPending(false);
    }
  }

  if (recovery) {
    return (
      <Shell>
        <Card className="flex flex-col gap-5">
          <div>
            <Eyebrow className="!text-leaf">Recovered</Eyebrow>
            <h1 className="mt-2 font-display text-4xl font-black leading-tight">Welcome back, {recovery.player}</h1>
            <p className="mt-2 text-muted">
              That code is now used up. Save your new one before you carry on, then choose a new PIN. Whatever signed you
              in before has been signed out.
            </p>
          </div>
          <RecoveryCodeCard code={recovery.recoveryCode} heading="Your new recovery code" />
          <label className="flex min-h-11 cursor-pointer items-start gap-3">
            <input type="checkbox" checked={saved} onChange={(e) => setSaved(e.target.checked)} className="mt-1 size-5 shrink-0 accent-[var(--accent)]" />
            <span className="font-semibold">I have saved my new recovery code</span>
          </label>
          <Button variant="primary" size="lg" disabled={!saved} onClick={() => redirect(claimPath(slug, recovery.inviteToken))}>
            Choose a new PIN <ArrowRightIcon />
          </Button>
        </Card>
      </Shell>
    );
  }

  return (
    <Shell>
      <div className="flex flex-col gap-8">
        <header>
          <Eyebrow>Host recovery</Eyebrow>
          <h1 className="mt-2 font-display text-5xl font-black leading-[0.95] tracking-tight">Forgot your host PIN?</h1>
          <p className="mt-4 text-lg text-muted">
            Enter the recovery code you were shown when you made this room. It signs out any device that is signed in as
            you and lets you choose a new PIN. Not the host? Ask the host to reset your seat instead.
          </p>
        </header>

        <form onSubmit={submit} className="flex flex-col gap-6">
          <Field id="recovery-code" label="Recovery code" hint="Capital letters and dashes do not matter.">
            <input
              id="recovery-code"
              value={code}
              onChange={(e) => setCode(e.target.value)}
              autoComplete="off"
              autoCapitalize="characters"
              spellCheck={false}
              placeholder="XXXX-XXXX-XXXX-XXXX"
              aria-describedby="recovery-code-hint"
              className={`${inputClass} min-h-14 font-mono text-xl tracking-widest`}
            />
          </Field>
          {error && <Alert>{error}</Alert>}
          <Button type="submit" variant="accent" size="lg" disabled={code.trim().length === 0 || pending}>
            {pending ? "Checking..." : "Recover my seat"} <ArrowRightIcon />
          </Button>
          <AppLink href={`/room/${slug}`} className="min-h-11 self-start py-2 font-semibold underline">
            Back to the join page
          </AppLink>
        </form>
      </div>
    </Shell>
  );
}
