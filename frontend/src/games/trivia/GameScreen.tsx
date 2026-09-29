import { CheckIcon, CloseIcon } from "../../components/icons";
import { Scoreboard } from "../../components/Scoreboard";
import { Eyebrow } from "../../components/ui";
import { formatTime } from "../../lib/format";
import type { GameScreenProps } from "../types";
import type { TriviaActivityView, TriviaPayload, TriviaReveal } from "./types";

const LETTERS = "ABCDEFGH";

function RevealCard({ reveal }: { reveal: TriviaReveal }) {
  return (
    <div className="animate-stamp rounded-xl border-2 border-ink bg-leaf-soft px-4 py-3 shadow-ticket-sm">
      <Eyebrow className="!text-ink">Previous question</Eyebrow>
      <p className="mt-1 font-semibold">{reveal.text}</p>
      <p className="mt-1 flex items-center gap-2 text-sm">
        <CheckIcon className="size-4 text-leaf" />
        <span>
          Correct answer: <strong>{reveal.options[reveal.correctIndex]}</strong>
        </span>
      </p>
    </div>
  );
}

function ActivityLog({ activity }: { activity: TriviaActivityView[] }) {
  return (
    <section aria-labelledby="activity-heading">
      <h2 id="activity-heading" className="font-mono text-sm font-bold uppercase tracking-widest">
        Activity
      </h2>
      {activity.length === 0 ? (
        <p className="mt-3 text-muted">No answers yet.</p>
      ) : (
        <ol aria-live="polite" className="mt-3 flex flex-col gap-2">
          {activity.map((a) => (
            <li key={a.answerId} className="flex items-start gap-3 rounded-lg border-2 border-ink bg-card px-3 py-2">
              <span
                className={`mt-0.5 grid size-6 shrink-0 place-items-center rounded-full border-2 border-ink ${a.correct ? "bg-leaf text-white" : "bg-tomato text-white"}`}
              >
                {a.correct ? <CheckIcon className="size-3.5" /> : <CloseIcon className="size-3.5" />}
              </span>
              <div>
                <p className="font-mono text-xs text-muted">
                  {formatTime(a.timestamp)} · question {a.questionNumber}
                </p>
                <p>
                  <strong>{a.triggeredBy}</strong> answered {a.correct ? "correctly" : "incorrectly"}
                </p>
              </div>
            </li>
          ))}
        </ol>
      )}
    </section>
  );
}

export function TriviaGameScreen({ me, players, session, payload, busy, onAction }: GameScreenProps<TriviaPayload>) {
  const myAnswered = payload.answered[me] ?? false;
  const answeredCount = Object.values(payload.answered).filter(Boolean).length;
  const progress = payload.totalQuestions > 0 ? Math.round(((payload.questionNumber - (payload.currentQuestion ? 1 : 0)) / payload.totalQuestions) * 100) : 0;

  return (
    <>
      {payload.lastReveal && <RevealCard reveal={payload.lastReveal} />}

      {payload.currentQuestion ? (
        <section aria-labelledby="question-heading" className="rounded-xl border-2 border-ink bg-card p-5 shadow-ticket sm:p-6">
          <p id="question-heading" className="font-mono text-xs font-bold uppercase tracking-widest text-muted">
            Question {payload.questionNumber} of {payload.totalQuestions}
          </p>
          <div
            role="progressbar"
            aria-label="Quiz progress"
            aria-valuemin={0}
            aria-valuemax={100}
            aria-valuenow={progress}
            className="mt-2 h-2.5 overflow-hidden rounded-full border-2 border-ink bg-paper"
          >
            <div className="h-full bg-accent transition-[width] duration-500" style={{ width: `${progress}%` }} />
          </div>
          <h3 className="mt-4 font-display text-2xl font-black leading-tight sm:text-3xl">{payload.currentQuestion.text}</h3>
          <div className="mt-5 grid grid-cols-1 gap-3 sm:grid-cols-2">
            {payload.currentQuestion.options.map((option, i) => (
              <button
                key={i}
                type="button"
                disabled={myAnswered || session.status !== "Active" || busy}
                onClick={() => onAction("answer", { optionIndex: i })}
                className="flex min-h-14 items-center gap-3 rounded-lg border-2 border-ink bg-paper px-3 text-left text-lg font-bold shadow-ticket-sm transition hover:bg-accent-soft active:translate-x-0.5 active:translate-y-0.5 active:shadow-none disabled:cursor-not-allowed disabled:opacity-60"
              >
                <span className="grid size-8 shrink-0 place-items-center rounded-md border-2 border-ink bg-accent font-mono text-sm font-bold text-on-accent">
                  {LETTERS[i]}
                </span>
                {option}
              </button>
            ))}
          </div>
          <p aria-live="polite" className="mt-4 text-sm text-muted">
            {myAnswered ? "Answer locked in - waiting on the others." : session.status === "Active" ? "Pick one." : "Waiting for the host to start."} ·{" "}
            {answeredCount}/{players.length} answered
          </p>
        </section>
      ) : (
        <section className="surface-dark animate-stamp rounded-xl border-2 border-ink bg-ink p-6 text-center text-paper shadow-ticket">
          <p className="font-mono text-xs uppercase tracking-[0.3em] text-mustard">Trivia complete</p>
          <p className="mt-2 font-display text-3xl font-black sm:text-4xl">All {payload.totalQuestions} questions answered</p>
        </section>
      )}

      <Scoreboard rows={payload.scoreboard.map((s) => ({ name: s.player, score: s.correct }))} me={me} unit="correct" />
      <ActivityLog activity={payload.activity} />
    </>
  );
}
