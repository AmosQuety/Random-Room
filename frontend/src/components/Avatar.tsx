// A fixed palette (not name-keyed, since players are no longer a fixed set) - all pairs clear 4.5:1 on their text.
const PALETTE = ["bg-tomato text-white", "bg-sky text-white", "bg-leaf text-white", "bg-mustard text-ink"];

function paletteIndex(name: string): number {
  let hash = 0;
  for (let i = 0; i < name.length; i++) hash = (hash * 31 + name.charCodeAt(i)) | 0;
  return Math.abs(hash) % PALETTE.length;
}

export function Avatar({ name }: { name: string }) {
  return (
    <span
      aria-hidden="true"
      className={`grid size-10 shrink-0 place-items-center rounded-full border-2 border-ink font-display text-lg font-bold ${PALETTE[paletteIndex(name)]}`}
    >
      {name[0]?.toUpperCase()}
    </span>
  );
}
