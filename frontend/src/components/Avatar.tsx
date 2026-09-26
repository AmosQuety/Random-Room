const COLORS: Record<string, string> = {
  Amos: "bg-tomato text-white",
  Lydia: "bg-sky text-white",
  James: "bg-leaf text-white",
  Jacob: "bg-mustard text-ink",
};

// James and Jacob share an initial, so colour separates them at a glance and the name always sits beside the avatar.
export function Avatar({ name }: { name: string }) {
  return (
    <span
      aria-hidden="true"
      className={`grid size-10 shrink-0 place-items-center rounded-full border-2 border-ink font-display text-lg font-bold ${COLORS[name] ?? "bg-card"}`}
    >
      {name[0]}
    </span>
  );
}
