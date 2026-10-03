import { useId, useState, type FormEvent } from "react";
import { CATEGORIES } from "../games/categories";
import type { Accent, GameCategory } from "../games/types";
import { navigate } from "../lib/router";
import { CreateRoomScreen } from "./CreateRoomScreen";
import { ArrowRightIcon } from "./icons";
import { HeroWordmark } from "./Logo";
import { Shell } from "./Shell";
import { Button, Eyebrow, Field } from "./ui";
import { buttonClass, inputClass } from "./styles";

const CATEGORY_ACCENT: Record<GameCategory, { accent: Accent; tilt: string }> = {
  poll: { accent: "tomato", tilt: "-rotate-2" },
  quiz: { accent: "sky", tilt: "rotate-1" },
  word: { accent: "plum", tilt: "-rotate-1" },
  reflex: { accent: "mustard", tilt: "rotate-2" },
  story: { accent: "leaf", tilt: "-rotate-2" },
};

const HOW_IT_WORKS = [
  { title: "Pick a game", body: "Polls, quizzes, drawing, reflex and story games for any size of group." },
  { title: "Send the links", body: "Every player gets a private invite link and sets their own PIN." },
  { title: "Play together", body: "Everyone sees the same live room. The server keeps it honest." },
] as const;

function JoinByCode() {
  const [code, setCode] = useState("");
  const id = useId();

  function submit(event: FormEvent) {
    event.preventDefault();
    const slug = code.trim().toLowerCase();
    if (slug) navigate(`/room/${slug}/join`);
  }

  return (
    <form onSubmit={submit} id="join" className="flex flex-col gap-3 rounded-xl border-2 border-dashed border-ink p-5">
      <Field id={id} label="Already have a room code?" hint="Paste the code from your invite, then pick your name.">
        <div className="flex gap-2">
          <input
            id={id}
            value={code}
            onChange={(e) => setCode(e.target.value)}
            placeholder="e.g. a1b2c3"
            aria-describedby={`${id}-hint`}
            autoCapitalize="none"
            autoCorrect="off"
            spellCheck={false}
            className={`${inputClass} font-mono tracking-widest`}
          />
          <Button type="submit" variant="secondary" disabled={!code.trim()}>
            Join
          </Button>
        </div>
      </Field>
    </form>
  );
}

export function HomeScreen() {
  return (
    <Shell width="wide">
      <div className="flex flex-col gap-14 sm:gap-20">
        <section aria-label="Welcome" className="flex flex-col gap-7">
          <HeroWordmark />
          <p className="max-w-xl text-xl leading-snug sm:text-2xl">
            Party games for reunions, cell groups and game nights. Everyone plays live from their own phone.
          </p>
          <div className="flex flex-wrap gap-3">
            <a href="#create" className={buttonClass("primary", "lg")}>
              Start a game <ArrowRightIcon />
            </a>
            <a href="#join" className={buttonClass("secondary", "lg")}>
              Join with a code
            </a>
          </div>
          <ul aria-label="Game types" className="flex flex-wrap gap-2.5">
            {CATEGORIES.map(({ key, label }) => (
              <li
                key={key}
                data-accent={CATEGORY_ACCENT[key].accent}
                className={`${CATEGORY_ACCENT[key].tilt} rounded-md border-2 border-ink bg-accent px-3 py-1.5 font-mono text-xs font-bold uppercase tracking-widest text-on-accent shadow-ticket-sm`}
              >
                {label}
              </li>
            ))}
          </ul>
        </section>

        <section aria-labelledby="how-heading">
          <h2 id="how-heading" className="sr-only">
            How it works
          </h2>
          <ol className="grid gap-4 sm:grid-cols-3">
            {HOW_IT_WORKS.map(({ title, body }, i) => (
              <li key={title} className="rounded-xl border-2 border-ink bg-card p-4 shadow-ticket-sm">
                <Eyebrow className="!text-tomato">Step {i + 1}</Eyebrow>
                <p className="mt-1 font-display text-xl font-black">{title}</p>
                <p className="mt-1 text-sm text-muted">{body}</p>
              </li>
            ))}
          </ol>
        </section>

        <section id="create" aria-labelledby="create-title" className="scroll-mt-6">
          <Eyebrow>Ready when you are</Eyebrow>
          <h2 id="create-title" className="mb-6 font-display text-4xl font-black leading-none sm:text-5xl">
            Start a game
          </h2>
          <CreateRoomScreen />
        </section>

        <JoinByCode />
      </div>
    </Shell>
  );
}
