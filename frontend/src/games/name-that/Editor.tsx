import { fieldInputClass } from "../../components/styles";
import type { PromptEditorProps } from "../rounds/RoundSetupForm";
import { isHttpsLink, MAX_ANSWERS, type IntroInput } from "./kit";

export function IntroEditor({ value, onChange, index, invalid }: PromptEditorProps<IntroInput>) {
  const setAnswer = (i: number, text: string) => onChange({ ...value, answers: value.answers.map((a, j) => (j === i ? text : a)) });
  const linkBad = value.link.trim() !== "" && !isHttpsLink(value.link);

  return (
    <div className="flex flex-col gap-2 rounded-lg border-2 border-ink bg-paper p-3">
      <input
        value={value.clue}
        maxLength={200}
        aria-label={`Clue ${index + 1}`}
        aria-invalid={invalid && value.clue.trim() === ""}
        placeholder="A clue: emojis, a riddle, a description"
        onChange={(e) => onChange({ ...value, clue: e.target.value })}
        className={fieldInputClass}
      />
      <input
        value={value.link}
        maxLength={300}
        inputMode="url"
        aria-label={`Link for clue ${index + 1} (optional)`}
        aria-invalid={linkBad}
        placeholder="Optional https:// link to a clip"
        onChange={(e) => onChange({ ...value, link: e.target.value })}
        className={fieldInputClass}
      />
      {linkBad && <p className="text-sm font-semibold text-tomato">A link must start with https://</p>}
      {value.answers.map((answer, i) => (
        <input
          key={i}
          value={answer}
          maxLength={80}
          aria-label={`Accepted answer ${i + 1} for clue ${index + 1}`}
          aria-invalid={invalid && i === 0 && value.answers.every((a) => a.trim() === "")}
          placeholder={i === 0 ? "Accepted answer" : "Another accepted answer (optional)"}
          onChange={(e) => setAnswer(i, e.target.value)}
          className={fieldInputClass}
        />
      ))}
      {value.answers.length < MAX_ANSWERS && (
        <button
          type="button"
          onClick={() => onChange({ ...value, answers: [...value.answers, ""] })}
          className="min-h-11 self-start rounded-lg px-2 font-mono text-sm font-bold uppercase tracking-widest text-muted underline"
        >
          + Another accepted answer
        </button>
      )}
    </div>
  );
}
