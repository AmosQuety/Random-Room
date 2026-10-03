// Verifies every text/background pairing the UI relies on against WCAG AA.
// Colors are read from the @theme block in src/index.css so this cannot drift from the real tokens.
import { readFileSync } from "node:fs";

const css = readFileSync(new URL("../src/index.css", import.meta.url), "utf8");
const tokens = Object.fromEntries([...css.matchAll(/--color-([a-z-]+):\s*(#[0-9a-fA-F]{6})/g)].map((m) => [m[1], m[2]]));
tokens.white = "#ffffff";

const channel = (v) => {
  const c = v / 255;
  return c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4;
};
const rgb = (hex) => [1, 3, 5].map((i) => parseInt(hex.slice(i, i + 2), 16));
const luminance = (hex) => {
  const [r, g, b] = rgb(hex).map(channel);
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
};
const blend = (fg, bg, alpha) => {
  const mixed = rgb(fg).map((v, i) => Math.round(v * alpha + rgb(bg)[i] * (1 - alpha)));
  return `#${mixed.map((v) => v.toString(16).padStart(2, "0")).join("")}`;
};
const ratio = (a, b) => {
  const [hi, lo] = [luminance(a), luminance(b)].sort((x, y) => y - x);
  return (hi + 0.05) / (lo + 0.05);
};

const accents = ["tomato", "sky", "leaf", "plum"];
const surfaces = ["paper", "card", "paper-deep"];
const pairs = [];
const add = (fg, bg, min, label) => pairs.push({ fg, bg, min, label });

for (const s of surfaces) {
  add(tokens.ink, tokens[s], 4.5, `ink on ${s}`);
  add(tokens.muted, tokens[s], 4.5, `muted on ${s}`);
  for (const a of [...accents, "mustard-ink"]) add(tokens[a], tokens[s], 4.5, `${a} text on ${s}`);
}
for (const a of [...accents, "mustard"]) {
  const soft = tokens[`${a}-soft`];
  add(tokens.ink, soft, 4.5, `ink on ${a}-soft`);
  add(tokens.muted, soft, 4.5, `muted on ${a}-soft`);
}
for (const a of accents) add(tokens.white, tokens[a], 4.5, `white on ${a}`);
add(tokens.ink, tokens.mustard, 4.5, "ink on mustard");
add(tokens.paper, tokens.ink, 4.5, "paper on ink");
add(blend(tokens.paper, tokens.ink, 0.8), tokens.ink, 4.5, "paper/80 on ink");
add(tokens.mustard, tokens.ink, 4.5, "mustard on ink");
add(tokens.white, tokens.muted, 4.5, "white on muted (disabled buttons)");
// Non-text: focus ring and control borders need 3:1 against what they sit on.
add(tokens.sky, tokens.paper, 3, "focus ring (sky) on paper");
add(tokens.sky, tokens.card, 3, "focus ring (sky) on card");
add(tokens.mustard, tokens.ink, 3, "focus ring (mustard) on ink");
add(tokens.ink, tokens.card, 3, "ink border on card");

let failed = 0;
for (const { fg, bg, min, label } of pairs) {
  const r = ratio(fg, bg);
  const ok = r >= min;
  if (!ok) failed++;
  console.log(`${ok ? "PASS" : "FAIL"}  ${r.toFixed(2).padStart(5)}:1 (need ${min})  ${label}`);
}
if (failed) {
  console.error(`\n${failed} pairing(s) below WCAG AA.`);
  process.exit(1);
}
console.log(`\nAll ${pairs.length} pairings pass.`);
