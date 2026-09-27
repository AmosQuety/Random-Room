import { formatTime } from "../../lib/format";
import type { GameScreenProps } from "../types";
import type { TriviaActivityView, TriviaPayload, TriviaReveal, TriviaScore } from "./types";

function RevealCard({ reveal }: { reveal: TriviaReveal }) {
  return (
    <div className="animate-stamp rounded-lg border-2 border-ink bg-paper px-4 py-3">
      <p className="font-mono text-xs uppercase tracking-widest text-muted">Previous question</p>
      <p className="mt-1 font-semibold">{reveal.text}</p>
      <p className="mt-1 text-sm">
        Correct answer: <strong className="text-leaf">{reveal.options[reveal.correctIndex]}</strong>
      </p>
    </div>
  );
}

function Scoreboard({ scoreboard, me }: { scoreboard: TriviaScore[]; me: string }) {
  return (
    <section aria-labelledby="scoreboard-heading">
      <h2 id="scoreboard-heading" className="mb-3 font-mono text-sm uppercase tracking-widest">
        Scoreboard
      </h2>
      <ol className="flex flex-col gap-2">
        {scoreboard.map((s, i) => (
          <li
            key={s.player}
            className={`flex items-center justify-between rounded-lg border-2 border-ink px-4 py-2 shadow-ticket-sm ${s.player === me ? "bg-card ring-4 ring-mustard/60" : "bg-card"}`}
          >
            <span className="font-display text-lg font-bold">
              {i + 1}. {s.player}
              {s.player === me && <span className="ml-2 font-mono text-xs font-normal uppercase text-muted">(you)</span>}
            </span>
            <span className="font-display text-2xl font-black text-tomato">{s.correct}</span>
          </li>
        ))}
      </ol>
    </section>
  );
}

function ActivityLog({ activity }: { activity: TriviaActivityView[] }) {
  return (
    <section aria-labelledby="activity-heading">
      <h2 id="activity-heading" className="font-mono text-sm uppercase tracking-widest">
        Activity
      </h2>
      {activity.length === 0 ? (
        <p className="mt-3 text-muted">No answers yet.</p>
      ) : (
        <ol aria-live="polite" className="mt-3 flex flex-col gap-2">
          {activity.map((a) => (
            <li key={a.answerId} className="rounded-lg border-2 border-ink bg-card px-3 py-2">
              <p className="font-mono text-xs text-muted">
                {formatTime(a.timestamp)} · question {a.questionNumber}
              </p>
              <p>
                {a.correct ? "✅" : "❌"} <strong>{a.triggeredBy}</strong> answered {a.correct ? "correctly" : "incorrectly"}
              </p>
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

  return (
    <>
      {payload.lastReveal && <RevealCard reveal={payload.lastReveal} />}

      {payload.currentQuestion ? (
        <section aria-labelledby="question-heading" className="rounded-xl border-2 border-ink bg-card p-6 shadow-ticket">
          <p id="question-heading" className="font-mono text-xs uppercase tracking-widest text-muted">
            Question {payload.questionNumber} of {payload.totalQuestions}
          </p>
          <h2 className="mt-2 font-display text-2xl font-black">{payload.currentQuestion.text}</h2>
          <div className="mt-4 grid grid-cols-1 gap-3 sm:grid-cols-2">
            {payload.currentQuestion.options.map((option, i) => (
              <button
                key={i}
                type="button"
                disabled={myAnswered || session.status !== "Active" || busy}
                onClick={() => onAction("answer", { optionIndex: i })}
                className="min-h-14 rounded-lg border-2 border-ink bg-paper px-4 text-left text-lg font-bold shadow-ticket-sm transition active:translate-x-0.5 active:translate-y-0.5 active:shadow-none disabled:cursor-not-allowed disabled:opacity-60"
              >
                {option}
              </button>
            ))}
          </div>
          <p className="mt-4 text-sm text-muted">
            {myAnswered ? "Answer locked in - waiting on the others." : "Pick one."} · {answeredCount}/{players.length} answered
          </p>
        </section>
      ) : (
        <section className="animate-stamp rounded-xl border-4 border-ink bg-ink p-6 text-center text-paper shadow-ticket">
          <p className="font-mono text-xs uppercase tracking-[0.3em] text-mustard">Trivia complete</p>
          <p className="mt-2 font-display text-3xl font-black sm:text-4xl">
            🎉 All {payload.totalQuestions} questions answered 🎉
          </p>
        </section>
      )}

      <Scoreboard scoreboard={payload.scoreboard} me={me} />
      <ActivityLog activity={payload.activity} />
    </>
  );
}
